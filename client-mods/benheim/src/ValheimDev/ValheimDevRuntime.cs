using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.ValheimDev;

internal sealed class ValheimDevPendingRequest
{
    private readonly ManualResetEventSlim completed = new ManualResetEventSlim(false);
    private string response = string.Empty;
    private volatile bool canceled;

    internal ValheimDevPendingRequest(ValheimDevRequest request) => Request = request;
    internal ValheimDevRequest Request { get; }

    internal bool IsCanceled => canceled;

    internal void Cancel() => canceled = true;

    internal void Complete(string value)
    {
        if (canceled) return;
        response = value;
        completed.Set();
    }

    internal bool Wait(int milliseconds) => completed.Wait(milliseconds);
    internal string Response => response;
}

internal sealed class ValheimDevActiveOperation
{
    internal ValheimDevPendingRequest Pending { get; set; } = null!;
    internal ValheimDevResponse Response { get; set; } = null!;
    internal DateTime DeadlineUtc { get; set; }
    internal HashSet<string> Selectors { get; } = new HashSet<string>(StringComparer.Ordinal);
    internal HashSet<string> ObservedSelectors { get; } = new HashSet<string>(StringComparer.Ordinal);
    internal int EvidenceBytes { get; set; }
    internal ValheimDevLoadedExperiment? Experiment { get; set; }
}

internal static partial class ValheimDevRuntime
{
    private const string SessionDirectoryName = "ValheimDev";
    private const string DescriptorFileName = "session.json";
    private static readonly object Gate = new object();
    private static readonly Queue<ValheimDevPendingRequest> Requests = new Queue<ValheimDevPendingRequest>();
    private static readonly Queue<DiagnosticEvent> PendingEvidence = new Queue<DiagnosticEvent>();
    private static string bepinExRootPath = string.Empty;
    private static string benheimVersion = string.Empty;
    private static int mainThreadId;
    private static bool initialized;
    private static volatile bool authorized;
    private static volatile bool cancellationRequested = true;
    private static bool wrongThreadLogged;
    private static ValheimDevWorldCapture? capture;
    private static ValheimDevBuildIdentity identity = new ValheimDevBuildIdentity();
    private static TcpListener? listener;
    private static Thread? acceptThread;
    private static int activeConnections;
    private static volatile bool listenerFailed;
    private static ValheimDevActiveOperation? activeOperation;
    private static bool restartRequired;
#if VALHEIM_DEV_TESTS
    private static Func<ValheimDevWorldState>? snapshotOverride;
    private static Func<ValheimDevBuildIdentity>? buildIdentityOverride;
#endif

    internal static bool IsCancellationRequested => cancellationRequested;
    internal static string DescriptorPath => Path.Combine(bepinExRootPath, SessionDirectoryName, DescriptorFileName);

#if VALHEIM_DEV_TESTS
    internal static bool IsAuthorizedForTests => authorized;
    internal static int QueueCountForTests
    {
        get { lock (Gate) return Requests.Count; }
    }

    internal static void SetTestHooks(
        Func<ValheimDevWorldState> snapshot,
        Func<ValheimDevBuildIdentity> buildIdentity)
    {
        snapshotOverride = snapshot;
        buildIdentityOverride = buildIdentity;
    }
#endif

    internal static void Initialize(string rootPath, string version, int unityMainThreadId)
    {
        bepinExRootPath = rootPath;
        benheimVersion = version;
        mainThreadId = unityMainThreadId;
        initialized = true;
        cancellationRequested = true;
        restartRequired = false;
        activeOperation = null;
        lock (Gate) PendingEvidence.Clear();
        DeleteDescriptor();
    }

    internal static bool TryHandleConsole(string[] arguments, Terminal context)
    {
        if (arguments.Length < 2
            || !string.Equals(arguments[0], "bh", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(arguments[1], "lab", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (arguments.Length != 3)
        {
            PrintUsage(context);
            return true;
        }

        switch (arguments[2].ToLowerInvariant())
        {
            case "on":
                if (TryAuthorize(out string authorizationResult))
                {
                    context.AddString($"Benheim Lab authorized for this local world session on 127.0.0.1:{((IPEndPoint)listener!.LocalEndpoint).Port}.");
                }
                else
                {
                    context.AddString($"Benheim Lab unavailable: {authorizationResult}.");
                }
                return true;
            case "off":
                Revoke("console_off");
                context.AddString("Benheim Lab authorization revoked.");
                return true;
            case "status":
                if (authorized)
                {
                    string reason = capture == null
                        ? "not_authorized"
                        : ValheimDevEligibility.CheckCapturedSession(capture, Snapshot());
                    context.AddString(reason == "eligible"
                        ? $"Benheim Lab is authorized for session {identity.SessionId}."
                        : $"Benheim Lab is revoking because the session is no longer eligible: {reason}.");
                    if (reason != "eligible") Revoke("status_drift:" + reason);
                }
                else context.AddString("Benheim Lab is off.");
                return true;
            default:
                PrintUsage(context);
                return true;
        }
    }

    internal static void PrintUsage(Terminal context)
    {
        context.AddString("  bh lab on|off|status");
    }

    internal static void Update()
    {
        if (!initialized) return;
        if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
        {
            if (!wrongThreadLogged)
            {
                wrongThreadLogged = true;
                Plugin.Log.LogError("Benheim Lab refused to execute outside Unity's main thread.");
            }
            return;
        }

        if (authorized && listenerFailed)
        {
            Revoke("listener_failed");
            return;
        }

        if (authorized && capture != null)
        {
            string drift = ValheimDevEligibility.CheckCapturedSession(capture, Snapshot());
            if (drift != "eligible")
            {
                Revoke("session_drift:" + drift);
                return;
            }
        }

        if (activeOperation != null)
        {
            DrainEvidence();
            if (cancellationRequested)
            {
                FinishActiveOperation("authorization_revoked", ok: false);
            }
            else if (activeOperation.ObservedSelectors.IsSupersetOf(activeOperation.Selectors)
                || DateTime.UtcNow >= activeOperation.DeadlineUtc)
            {
                FinishActiveOperation(null, ok: activeOperation.Response.Exception == null);
            }
            return;
        }

        ValheimDevPendingRequest? pending = null;
        lock (Gate)
        {
            if (Requests.Count > 0) pending = Requests.Dequeue();
        }
        if (pending == null) return;
        if (!pending.IsCanceled) Process(pending);
    }

    internal static void Revoke(string reason)
    {
        cancellationRequested = true;
        bool wasAuthorized = authorized;
        authorized = false;
        StopListener();
        DeleteDescriptor();

        string cleanupState = ValheimDevCleanupState.NotApplicable;
        if (activeOperation != null)
        {
            cleanupState = FinishActiveOperation("authorization_revoked", ok: false);
        }

        List<ValheimDevPendingRequest> canceled = new List<ValheimDevPendingRequest>();
        lock (Gate)
        {
            while (Requests.Count > 0) canceled.Add(Requests.Dequeue());
        }
        foreach (ValheimDevPendingRequest pending in canceled)
        {
            ValheimDevResponse response = ResponseFor(pending.Request);
            response.Error = "authorization_revoked";
            pending.Complete(response.ToJson(pending.Request.Kind == "apply"));
        }

        Diagnostics.SetValheimDevObserver(null);
        capture = null;
        if (wasAuthorized)
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("ValheimDev", "lab_revoked")
                    .String("reason", reason)
                    .String("cleanup_state", cleanupState));
        }
    }

    private static bool TryAuthorize(out string result)
    {
        if (!initialized)
        {
            result = "not_initialized";
            return false;
        }
        if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
        {
            result = "not_main_thread";
            return false;
        }

        if (restartRequired)
        {
            result = ValheimDevCleanupState.RestartRequired;
            return false;
        }

        if (authorized)
        {
            result = "already_authorized";
            return true;
        }

        ValheimDevWorldState state = Snapshot();
        string eligibility = ValheimDevEligibility.CheckAuthorization(state);
        if (eligibility != "eligible")
        {
            result = eligibility;
            return false;
        }

        TcpListener candidate = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            candidate.Start(ValheimDevProtocol.MaximumQueueDepth);
            ValheimDevBuildIdentity candidateIdentity = BuildIdentity();
            candidateIdentity.SessionId = Guid.NewGuid().ToString("N");
            candidateIdentity.Generation = Guid.NewGuid().ToString("N");
            candidateIdentity.Token = RandomHex(32);
            candidateIdentity.AuthorizedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            int port = ((IPEndPoint)candidate.LocalEndpoint).Port;
            WriteDescriptor(candidateIdentity, port);

            listener = candidate;
            listenerFailed = false;
            identity = candidateIdentity;
            capture = new ValheimDevWorldCapture
            {
                Network = state.Network!,
                Scene = state.Scene!,
                WorldId = state.WorldId
            };
            cancellationRequested = false;
            authorized = true;
            acceptThread = new Thread(() => AcceptLoop(candidate))
            {
                IsBackground = true,
                Name = "Benheim Valheim Dev listener"
            };
            acceptThread.Start();
            Diagnostics.Emit(
                DiagnosticEvent.Create("ValheimDev", "lab_authorized")
                    .String("session_id", candidateIdentity.SessionId)
                    .String("generation", candidateIdentity.Generation));
            result = "authorized";
            return true;
        }
        catch (Exception exception)
        {
            candidate.Stop();
            DeleteDescriptor();
            result = "listener_or_descriptor_failed:" + Diagnostics.Flatten(exception.Message);
            return false;
        }
    }

}

[HarmonyPatch(typeof(ZNet), "OnDestroy")]
internal static class ValheimDevZNetTeardownPatch
{
    private static void Prefix(ZNet __instance)
    {
        if (ReferenceEquals(__instance, ZNet.instance))
        {
            ValheimDevRuntime.Revoke("znet_teardown");
        }
    }
}
