using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ValheimDev;

internal sealed class ValheimDevAppPending
{
    private readonly object gate = new object();
    private readonly ManualResetEventSlim completed = new ManualResetEventSlim(false);
    private bool canceled;
    private bool started;
    private string response = string.Empty;
    internal ValheimDevAppPending(ValheimDevAppRequest request) => Request = request;
    internal ValheimDevAppRequest Request { get; }
    internal bool TryStart()
    {
        lock (gate)
        {
            if (canceled) return false;
            started = true;
            return true;
        }
    }
    internal string Cancel()
    {
        lock (gate)
        {
            canceled = true;
            return started ? "runtime_unresolved" : "main_thread_timeout";
        }
    }
    internal void Complete(string value) { response = value; completed.Set(); }
    internal bool Wait(int timeout) => completed.Wait(timeout);
    internal string Response => response;
}

// App control lives independently of Lab authorization: menu inspection cannot
// require an active in-world compiler/execution session.
internal sealed class ValheimDevAppRuntime : IDisposable
{
    private readonly object gate = new object();
    private readonly Queue<ValheimDevAppPending> requests = new Queue<ValheimDevAppPending>();
    private readonly TcpListener listener;
    private readonly ValheimDevAppController controller;
    private readonly string descriptorPath;
    private readonly int mainThreadId;
    private volatile bool disposed;
    private int activeConnections;
    private string lastObservationError = string.Empty;
    internal string AppId { get; } = Guid.NewGuid().ToString("N");
#if VALHEIM_DEV_TESTS
    internal int QueueCountForTests { get { lock (gate) return requests.Count; } }
#endif

    internal ValheimDevAppRuntime(string root, int mainThreadId, IValheimDevAppNative native,
        ValheimDevSessionIdentity identity, string launchId)
    {
        if (!Path.IsPathRooted(root)) throw new InvalidOperationException("app_data_root_must_be_absolute");
        this.mainThreadId = mainThreadId;
        descriptorPath = Path.Combine(root, "app.json");
        using Process process = Process.GetCurrentProcess();
        controller = new ValheimDevAppController(native, AppId, process.Id, launchId);
        listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start(ValheimDevAppProtocol.MaximumQueueDepth);
            WriteDescriptor(root, identity, process.Id, process.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), launchId);
            new Thread(AcceptLoop) { IsBackground = true, Name = "Valheim Dev app listener" }.Start();
        }
        catch
        {
            listener.Stop();
            DeleteDescriptor();
            throw;
        }
    }

    internal void Update()
    {
        if (disposed || Thread.CurrentThread.ManagedThreadId != mainThreadId) return;
        try { controller.Observe(); lastObservationError = string.Empty; }
        catch (Exception exception)
        {
            string error = BoundError(exception);
            if (error != lastObservationError)
            {
                lastObservationError = error;
                ValheimDevDiagnostics.Emit("app_observation_failed", new KeyValuePair<string, string>("error", error));
            }
        }
        ValheimDevAppPending? pending = null;
        lock (gate) { if (requests.Count > 0) pending = requests.Dequeue(); }
        if (pending == null || !pending.TryStart()) return;
        try { pending.Complete(controller.Process(pending.Request)); }
        catch (Exception exception)
        {
            pending.Complete(ValheimDevAppProtocol.Response(AppId, pending.Request.RequestId, "native_state_unavailable:" + BoundError(exception)));
        }
    }

    private void AcceptLoop()
    {
        while (!disposed)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                if (Interlocked.Increment(ref activeConnections) > ValheimDevAppProtocol.MaximumQueueDepth * 2)
                {
                    Interlocked.Decrement(ref activeConnections); client.Dispose(); continue;
                }
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
            catch (Exception exception) when (exception is SocketException || exception is ObjectDisposedException)
            {
                if (!disposed)
                {
                    disposed = true;
                    DeleteDescriptor();
                    ValheimDevDiagnostics.Emit("app_listener_failed", new KeyValuePair<string, string>("error", BoundError(exception)));
                }
                return;
            }
        }
    }

    private void HandleClient(TcpClient client)
    {
        ValheimDevAppRequest request = new ValheimDevAppRequest();
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 5000; client.SendTimeout = 5000;
                NetworkStream stream = client.GetStream();
                string json;
                try { json = ReadLine(stream); }
                catch (Exception exception) { Write(stream, ValheimDevAppProtocol.Response(AppId, string.Empty, "request_read_failed:" + BoundError(exception))); return; }
                if (!ValheimDevAppProtocol.TryParse(json, out request, out string error))
                { Write(stream, ValheimDevAppProtocol.Response(AppId, request.RequestId, error)); return; }
                if (request.AppId != AppId)
                { Write(stream, ValheimDevAppProtocol.Response(AppId, request.RequestId, "stale_app")); return; }
                ValheimDevAppPending pending = new ValheimDevAppPending(request);
                lock (gate)
                {
                    if (disposed) { Write(stream, ValheimDevAppProtocol.Response(AppId, request.RequestId, "app_stopped")); return; }
                    if (requests.Count >= ValheimDevAppProtocol.MaximumQueueDepth)
                    { Write(stream, ValheimDevAppProtocol.Response(AppId, request.RequestId, "queue_full")); return; }
                    requests.Enqueue(pending);
                }
                if (!pending.Wait(15000))
                { Write(stream, ValheimDevAppProtocol.Response(AppId, request.RequestId, pending.Cancel())); return; }
                Write(stream, pending.Response);
            }
        }
        catch (Exception exception) when (exception is IOException || exception is SocketException || exception is ObjectDisposedException)
        {
            // A disconnected caller never authorizes a retry of a mutation. Its
            // next status request must establish the resulting native state.
        }
        finally { Interlocked.Decrement(ref activeConnections); }
    }

    private static string ReadLine(Stream stream)
    {
        using MemoryStream bytes = new MemoryStream();
        while (bytes.Length <= ValheimDevAppProtocol.MaximumRequestBytes)
        {
            int value = stream.ReadByte();
            if (value < 0) throw new IOException("request_requires_newline");
            if (value == '\n') return new UTF8Encoding(false, true).GetString(bytes.ToArray());
            bytes.WriteByte((byte)value);
        }
        throw new IOException("request_too_large");
    }

    private static bool Write(Stream stream, string json)
    {
        try { byte[] bytes = Encoding.UTF8.GetBytes(json + "\n"); stream.Write(bytes, 0, bytes.Length); return true; }
        catch (Exception exception) when (exception is IOException || exception is SocketException || exception is ObjectDisposedException) { return false; }
    }

    public void Dispose()
    {
        disposed = true;
        listener.Stop();
        DeleteDescriptor();
        lock (gate)
        {
            while (requests.Count > 0)
            {
                ValheimDevAppPending pending = requests.Dequeue();
                pending.Cancel();
                pending.Complete(ValheimDevAppProtocol.Response(AppId, pending.Request.RequestId, "app_stopped"));
            }
        }
    }

    private void WriteDescriptor(string root, ValheimDevSessionIdentity identity, int pid, string started, string launchId)
    {
        Directory.CreateDirectory(root);
        StringBuilder json = new StringBuilder("{");
        ValheimDevJson.AppendProperty(json, "protocol", ValheimDevAppProtocol.Version);
        json.Append(','); ValheimDevJson.AppendProperty(json, "app_id", AppId);
        json.Append(','); ValheimDevJson.AppendProperty(json, "pid", pid);
        json.Append(','); ValheimDevJson.AppendProperty(json, "process_started_utc", started);
        json.Append(','); ValheimDevJson.AppendProperty(json, "host", "127.0.0.1");
        json.Append(','); ValheimDevJson.AppendProperty(json, "port", ((IPEndPoint)listener.LocalEndpoint).Port);
        json.Append(','); ValheimDevJson.AppendProperty(json, "data_root", Path.GetFullPath(root));
        json.Append(','); ValheimDevJson.AppendProperty(json, "launch_id", launchId);
        json.Append(','); ValheimDevJson.AppendProperty(json, "valheim_version", identity.ValheimVersion);
        json.Append(','); ValheimDevJson.AppendProperty(json, "valheim_sha256", identity.ValheimSha256);
        json.Append(','); ValheimDevJson.AppendProperty(json, "valheim_dev_version", identity.ValheimDevVersion);
        json.Append(','); ValheimDevJson.AppendProperty(json, "valheim_dev_sha256", identity.ValheimDevSha256);
        json.Append('}');
        string temporary = Path.Combine(root, ".app-" + AppId + ".tmp");
        try
        {
            File.WriteAllText(temporary, json.ToString(), new UTF8Encoding(false));
            if (File.Exists(descriptorPath)) File.Replace(temporary, descriptorPath, null);
            else File.Move(temporary, descriptorPath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private void DeleteDescriptor()
    {
        try
        {
            // Another process may have replaced the descriptor. Never remove
            // its published identity during this process's teardown.
            if (File.Exists(descriptorPath) && ValheimDevJson.TryParseObject(File.ReadAllText(descriptorPath), out var value, out _)
                && value.TryGetValue("app_id", out object? id) && id as string == AppId) File.Delete(descriptorPath);
        }
        catch (Exception exception)
        { Plugin.Log.LogWarning("Valheim Dev app descriptor cleanup failed: " + BoundError(exception)); }
    }

    private static string BoundError(Exception exception)
    {
        string error = ValheimDevDiagnostics.Flatten(exception.Message);
        return error.Length <= 256 ? error : error.Substring(0, 256);
    }
}
