using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BenheimQoL.Infrastructure;
using BepInEx;
using HarmonyLib;

namespace BenheimQoL.ValheimDev;

internal static partial class ValheimDevRuntime
{
    private static void AcceptLoop(TcpListener ownedListener)
    {
        while (authorized && ReferenceEquals(listener, ownedListener))
        {
            try
            {
                TcpClient client = ownedListener.AcceptTcpClient();
                if (Interlocked.Increment(ref activeConnections) > ValheimDevProtocol.MaximumQueueDepth * 2)
                {
                    Interlocked.Decrement(ref activeConnections);
                    client.Dispose();
                    continue;
                }
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
            catch (SocketException)
            {
                if (authorized && ReferenceEquals(listener, ownedListener))
                {
                    listenerFailed = true;
                    Plugin.Log.LogWarning("Benheim Lab listener stopped unexpectedly.");
                }
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private static void HandleClient(TcpClient client)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                NetworkStream stream = client.GetStream();
                string? line;
                try
                {
                    line = ReadBoundedLine(stream);
                }
                catch (Exception exception)
                {
                    WriteResponse(stream, ErrorJson("request_read_failed:" + Diagnostics.Flatten(exception.Message)));
                    return;
                }
                if (line == null)
                {
                    WriteResponse(stream, ErrorJson("request_missing"));
                    return;
                }
                if (!ValheimDevProtocol.TryParseRequest(line, out ValheimDevRequest request, out string parseError))
                {
                    WriteResponse(stream, ErrorJson(parseError));
                    return;
                }

                ValheimDevPendingRequest pending = new ValheimDevPendingRequest(request);
                lock (Gate)
                {
                    if (!authorized)
                    {
                    WriteResponse(stream, ErrorJson("not_authorized", request));
                        return;
                    }
                    if (Requests.Count >= ValheimDevProtocol.MaximumQueueDepth)
                    {
                        WriteResponse(stream, ErrorJson("queue_full", request));
                        return;
                    }
                    Requests.Enqueue(pending);
                }

                bool waitsForEvidence = request.Kind == "inspect" || request.Kind == "install_change";
                int wait = waitsForEvidence
                    ? Math.Min(ValheimDevProtocol.MaximumEvidenceTimeoutMs + 15000, request.EvidenceTimeoutMs + 15000)
                    : 15000;
                if (!pending.Wait(wait))
                {
                    pending.Cancel();
                    WriteResponse(stream, ErrorJson("main_thread_timeout", request));
                    return;
                }
                WriteResponse(stream, pending.Response);
            }
        }
        finally
        {
            Interlocked.Decrement(ref activeConnections);
        }
    }

    private static string? ReadBoundedLine(Stream stream)
    {
        using MemoryStream buffer = new MemoryStream();
        while (buffer.Length <= ValheimDevProtocol.MaximumRequestBytes)
        {
            int value = stream.ReadByte();
            if (value < 0) return buffer.Length == 0 ? null : throw new IOException("request must end with newline");
            if (value == '\n')
            {
                byte[] bytes = buffer.ToArray();
                if (bytes.Length > 0 && bytes[bytes.Length - 1] == '\r')
                {
                    Array.Resize(ref bytes, bytes.Length - 1);
                }
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            buffer.WriteByte((byte)value);
        }
        throw new IOException("request_too_large");
    }

    private static void WriteResponse(Stream stream, string json)
    {
        try
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(json + "\n");
            stream.Write(bytes, 0, bytes.Length);
        }
        catch
        {
            // The requester owns a closed connection; gameplay remains intact.
        }
    }

    private static string ErrorJson(string error, ValheimDevRequest? request = null)
    {
        ValheimDevResponse response = new ValheimDevResponse
        {
            Identity = identity,
            Authorized = authorized,
            RestartRequired = restartRequired,
            Action = request?.Kind ?? string.Empty,
            Error = error,
            OperationId = request?.OperationId ?? string.Empty,
            ChangeId = request?.ChangeId ?? string.Empty,
            EvidenceSelected = request?.EvidenceEvents.Count > 0,
            EvidenceExhaustive = false
        };
        SnapshotActiveChanges(response);
        return response.ToJson(includeOperation: request != null && request.Kind != "status");
    }

    private static void StopListener()
    {
        TcpListener? current = listener;
        listener = null;
        try { current?.Stop(); }
        catch { }
        acceptThread = null;
    }

    private static void WriteDescriptor(ValheimDevBuildIdentity value, int port)
    {
        string directory = Path.Combine(bepinExRootPath, SessionDirectoryName);
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, DescriptorFileName);
        string temporary = Path.Combine(directory, ".session-" + Guid.NewGuid().ToString("N") + ".tmp");
        StringBuilder builder = new StringBuilder(1024);
        builder.Append('{');
        ValheimDevJson.AppendProperty(builder, "protocol", ValheimDevProtocol.ProtocolVersion);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "session_id", value.SessionId);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "generation", value.Generation);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "token", value.Token);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "host", "127.0.0.1");
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "port", port);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "authorized_at", value.AuthorizedAt);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "valheim_version", value.ValheimVersion);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "valheim_sha256", value.ValheimSha256);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "benheim_version", value.BenheimVersion);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "benheim_sha256", value.BenheimSha256);
        builder.Append(',');
        ValheimDevJson.AppendStringArrayProperty(builder, "compiler_references", value.CompilerReferences);
        builder.Append('}');

        File.WriteAllText(temporary, builder.ToString(), new UTF8Encoding(false));
        try
        {
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void DeleteDescriptor()
    {
        if (string.IsNullOrEmpty(bepinExRootPath)) return;
        try
        {
            string path = DescriptorPath;
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception)
        {
            if (initialized) Plugin.Log.LogWarning("Benheim Lab could not remove its descriptor: " + Diagnostics.Flatten(exception.Message));
        }
    }

    private static ValheimDevBuildIdentity BuildIdentity()
    {
#if VALHEIM_DEV_TESTS
        if (buildIdentityOverride != null) return buildIdentityOverride();
#endif
        Assembly valheim = typeof(ZNet).Assembly;
        Assembly benheim = typeof(Plugin).Assembly;
        string valheimPath = Path.GetFullPath(valheim.Location);
        string benheimPath = Path.GetFullPath(benheim.Location);
        string managedDirectory = Path.GetDirectoryName(valheimPath)!;
        ValheimDevBuildIdentity value = new ValheimDevBuildIdentity
        {
            ValheimVersion = ReadValheimVersion(valheim),
            ValheimSha256 = Sha256(File.ReadAllBytes(valheimPath)),
            BenheimVersion = benheimVersion,
            BenheimSha256 = Sha256(File.ReadAllBytes(benheimPath))
        };
        string coreLibraryPath = Path.GetFullPath(typeof(object).Assembly.Location);
        string frameworkDirectory = Path.GetDirectoryName(coreLibraryPath)!;
        AddReference(value, coreLibraryPath);
        AddReference(value, Path.Combine(frameworkDirectory, "System.dll"));
        AddReference(value, Path.Combine(frameworkDirectory, "System.Core.dll"));
        AddReference(value, FindNetstandard(coreLibraryPath));
        AddReference(value, valheimPath);
        AddReference(value, Path.Combine(managedDirectory, "UnityEngine.dll"));
        AddReference(value, Path.Combine(managedDirectory, "UnityEngine.CoreModule.dll"));
        AddReference(value, typeof(BaseUnityPlugin).Assembly.Location);
        AddReference(value, typeof(Harmony).Assembly.Location);
        AddReference(value, benheimPath);
        if (value.CompilerReferences.Count != 10)
        {
            throw new InvalidOperationException("the curated compiler reference set is incomplete or contains duplicates");
        }
        return value;
    }

    private static void AddReference(ValheimDevBuildIdentity value, string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("compiler reference was not found", fullPath);
        if (!value.CompilerReferences.Contains(fullPath, StringComparer.Ordinal)) value.CompilerReferences.Add(fullPath);
    }

    private static string FindNetstandard(string coreLibraryPath)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(assembly.GetName().Name, "netstandard", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(assembly.Location)) return assembly.Location;
        }
        string directory = Path.GetDirectoryName(coreLibraryPath)!;
        string facade = Path.Combine(directory, "Facades", "netstandard.dll");
        if (File.Exists(facade)) return facade;
        return Path.Combine(directory, "netstandard.dll");
    }

    private static string ReadValheimVersion(Assembly valheimAssembly)
    {
        Type? versionType = valheimAssembly.GetType("Version", throwOnError: false, ignoreCase: false);
        MethodInfo? method = versionType?.GetMethod(
            "GetVersionString",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(bool) },
            modifiers: null);
        return method?.Invoke(null, new object[] { false }) as string
            ?? throw new InvalidOperationException("Valheim's exact version API is unavailable");
    }

    private static bool IsSha256(string value)
    {
        if (value.Length != 64) return false;
        foreach (char character in value)
        {
            bool hex = character >= '0' && character <= '9'
                || character >= 'a' && character <= 'f'
                || character >= 'A' && character <= 'F';
            if (!hex) return false;
        }
        return true;
    }

    private static string Sha256(byte[] bytes)
    {
        using SHA256 algorithm = SHA256.Create();
        byte[] hash = algorithm.ComputeHash(bytes);
        StringBuilder builder = new StringBuilder(64);
        foreach (byte value in hash) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static string RandomHex(int byteCount)
    {
        byte[] bytes = new byte[byteCount];
        using RandomNumberGenerator generator = RandomNumberGenerator.Create();
        generator.GetBytes(bytes);
        StringBuilder builder = new StringBuilder(byteCount * 2);
        foreach (byte value in bytes) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static bool ConstantTimeEquals(string left, string right)
    {
        byte[] a = Encoding.UTF8.GetBytes(left);
        byte[] b = Encoding.UTF8.GetBytes(right);
        int difference = a.Length ^ b.Length;
        int length = Math.Max(a.Length, b.Length);
        for (int index = 0; index < length; index++)
        {
            byte av = index < a.Length ? a[index] : (byte)0;
            byte bv = index < b.Length ? b[index] : (byte)0;
            difference |= av ^ bv;
        }
        return difference == 0;
    }
}
