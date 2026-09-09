using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using HarmonyLib;

namespace ValheimDev;

internal sealed class ValheimDevPendingRequest
{
    private readonly ManualResetEventSlim completed = new ManualResetEventSlim(false);
    private string response = string.Empty;
    private volatile bool canceled;

    internal ValheimDevPendingRequest(ValheimDevRequest request, ValheimDevSession session)
    {
        Request = request;
        Session = session;
    }
    internal ValheimDevRequest Request { get; }
    internal ValheimDevSession Session { get; }

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
    internal string CompletionCleanupState { get; set; } = ValheimDevCleanupState.NotApplicable;
}

internal sealed class ValheimDevManagedChange
{
    internal ValheimDevLoadedCode Code { get; set; } = null!;
    internal string InputJson { get; set; } = "{}";
    internal ValheimDevChangeSummary Summary { get; set; } = new ValheimDevChangeSummary();
}

internal sealed class ValheimDevSession
{
    // One object owns the resources that make Lab authorization real. Its
    // presence is the authorization state; there is no second flag to drift.
    internal ValheimDevSession(
        ValheimDevSessionIdentity identity,
        ValheimDevWorldCapture capture,
        TcpListener listener)
    {
        Identity = identity;
        Capture = capture;
        Listener = listener;
    }

    internal ValheimDevSessionIdentity Identity { get; }
    internal ValheimDevWorldCapture Capture { get; }
    internal TcpListener Listener { get; }
    internal volatile bool ListenerFailed;
}

internal static partial class ValheimDevRuntime
{
    private static readonly object Gate = new object();
    private static readonly Queue<ValheimDevPendingRequest> Requests = new Queue<ValheimDevPendingRequest>();
    private static readonly Queue<ValheimDevEvidenceRecord> PendingEvidence = new Queue<ValheimDevEvidenceRecord>();
    private static readonly Dictionary<string, ValheimDevManagedChange> ManagedChanges =
        new Dictionary<string, ValheimDevManagedChange>(StringComparer.Ordinal);
    private static readonly HashSet<string> RestartRequiredChanges =
        new HashSet<string>(StringComparer.Ordinal);
    private static string dataRoot = string.Empty;
    private static string logPath = string.Empty;
    private static string pluginVersion = string.Empty;
    private static int mainThreadId;
    private static bool initialized;
    private static bool wrongThreadLogged;
    private static int activeConnections;
    private static volatile ValheimDevSession? session;
    private static ValheimDevWorldCapture? trackedWorld;
    private static ValheimDevActiveOperation? activeOperation;
    private static volatile bool restartRequired;
#if VALHEIM_DEV_TESTS
    private static Func<ValheimDevWorldState>? snapshotOverride;
    private static Func<ValheimDevSessionIdentity>? sessionIdentityOverride;
#endif

    internal static bool IsCancellationRequested => trackedWorld == null;
    internal static string DataRoot => dataRoot;
    internal static string DescriptorPath => Path.Combine(dataRoot, "session.json");

#if VALHEIM_DEV_TESTS
    internal static bool IsAuthorizedForTests => session != null;
    internal static int ActiveConnectionCountForTests => Volatile.Read(ref activeConnections);
    internal static int QueueCountForTests
    {
        get { lock (Gate) return Requests.Count; }
    }

    internal static void SetTestHooks(
        Func<ValheimDevWorldState> snapshot,
        Func<ValheimDevSessionIdentity> sessionIdentity)
    {
        snapshotOverride = snapshot;
        sessionIdentityOverride = sessionIdentity;
    }
#endif

    internal static void Initialize(
        string rootPath,
        string resolvedLogPath,
        string version,
        int unityMainThreadId)
    {
        if (!Path.IsPathRooted(rootPath))
        {
            throw new InvalidOperationException("VALHEIM_DEV_ROOT must be absolute: " + rootPath);
        }
        dataRoot = Path.GetFullPath(rootPath);
        logPath = Path.GetFullPath(resolvedLogPath);
        pluginVersion = version;
        mainThreadId = unityMainThreadId;
        initialized = true;
        restartRequired = false;
        trackedWorld = null;
        activeOperation = null;
        lock (Gate)
        {
            ManagedChanges.Clear();
            RestartRequiredChanges.Clear();
            PendingEvidence.Clear();
        }
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
                    context.AddString($"Valheim Dev Lab authorized for this local world session on 127.0.0.1:{((IPEndPoint)session!.Listener.LocalEndpoint).Port}.");
                }
                else
                {
                    context.AddString($"Valheim Dev Lab unavailable: {authorizationResult}.");
                }
                return true;
            case "off":
                CloseAccess("console_off");
                string offMessage = ManagedChangeCount() == 0
                    ? "Valheim Dev Lab access is off."
                    : $"Valheim Dev Lab access is off. {ManagedChangeCount()} installed change(s) remain active in this world.";
                context.AddString(restartRequired
                    ? RestartRequiredMessage(offMessage + " Cleanup is uncertain and a game restart is required.")
                    : offMessage);
                return true;
            case "status":
                ValheimDevSession? current = session;
                if (current != null)
                {
                    string reason = ValheimDevEligibility.CheckCapturedSession(current.Capture, Snapshot());
                    if (reason == "eligible")
                    {
                        context.AddString(restartRequired
                            ? RestartRequiredMessage($"Valheim Dev Lab is authorized for session {current.Identity.SessionId} with {ManagedChangeCount()} active managed change(s), but cleanup is uncertain and a game restart is required.")
                            : $"Valheim Dev Lab is authorized for session {current.Identity.SessionId} with {ManagedChangeCount()} active managed change(s).");
                    }
                    else
                    {
                        string driftCleanup = Revoke("status_drift:" + reason);
                        context.AddString(restartRequired || driftCleanup == ValheimDevCleanupState.RestartRequired
                            ? RestartRequiredMessage($"Valheim Dev Lab revoked after session drift ({reason}), but cleanup is uncertain and a game restart is required.")
                            : $"Valheim Dev Lab revoked because the session is no longer eligible: {reason}.");
                    }
                }
                else
                {
                    if (trackedWorld != null
                        && ValheimDevEligibility.CheckCapturedSession(trackedWorld, Snapshot()) != "eligible")
                    {
                        Revoke("status_world_changed");
                    }
                    string statusMessage = ManagedChangeCount() == 0
                        ? "Valheim Dev Lab is off."
                        : $"Valheim Dev Lab is off with {ManagedChangeCount()} installed change(s) still registered for this world.";
                    context.AddString(restartRequired
                        ? RestartRequiredMessage(statusMessage + " Cleanup is uncertain and a game restart is required.")
                        : statusMessage);
                }
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
                Plugin.Log.LogError("Valheim Dev Lab refused to execute outside Unity's main thread.");
            }
            return;
        }

        ValheimDevSession? current = session;
        if (current != null && current.ListenerFailed)
        {
            CloseAccess("listener_failed");
            return;
        }

        if (current != null)
        {
            string drift = ValheimDevEligibility.CheckCapturedSession(current.Capture, Snapshot());
            if (drift != "eligible")
            {
                Revoke("session_drift:" + drift);
                return;
            }
        }

        ValheimDevActiveOperation? operation = activeOperation;
        if (operation != null)
        {
            DrainEvidence(operation);
            if (session == null)
            {
                FinishActiveOperation("authorization_revoked", ok: false, operation.CompletionCleanupState);
            }
            else if (operation.ObservedSelectors.IsSupersetOf(operation.Selectors)
                || DateTime.UtcNow >= operation.DeadlineUtc)
            {
                FinishActiveOperation(null, ok: operation.Response.Exception == null, operation.CompletionCleanupState);
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

    internal static string Revoke(string reason)
    {
        return EndAccess(reason, cleanupManagedChanges: true);
    }

    private static string CloseAccess(string reason)
    {
        return EndAccess(reason, cleanupManagedChanges: false);
    }

    private static string EndAccess(string reason, bool cleanupManagedChanges)
    {
        ValheimDevSession? revokedSession = session;
        session = null;
        StopListener(revokedSession);
        DeleteDescriptor();

        if (cleanupManagedChanges) trackedWorld = null;
        Dictionary<string, string> cleanupResults = cleanupManagedChanges
            ? CleanupManagedChanges()
            : new Dictionary<string, string>(StringComparer.Ordinal);
        string cleanupState = AggregateCleanupState(cleanupResults);
        if (activeOperation != null)
        {
            string operationCleanupState = cleanupManagedChanges
                ? CleanupStateForActiveOperation(cleanupResults)
                : activeOperation.CompletionCleanupState;
            FinishActiveOperation(
                "authorization_revoked",
                ok: false,
                operationCleanupState);
        }

        List<ValheimDevPendingRequest> canceled = new List<ValheimDevPendingRequest>();
        lock (Gate)
        {
            while (Requests.Count > 0) canceled.Add(Requests.Dequeue());
        }
        foreach (ValheimDevPendingRequest pending in canceled)
        {
            ValheimDevResponse response = ResponseFor(pending);
            response.Error = "authorization_revoked";
            pending.Complete(response.ToJson(pending.Request.Kind != "status"));
        }

        ValheimDevDiagnostics.UnsubscribeOptionalEvidence();
        if (revokedSession != null)
        {
            ValheimDevDiagnostics.Emit(
                "lab_revoked",
                new KeyValuePair<string, string>("lab_session_id", revokedSession.Identity.SessionId),
                new KeyValuePair<string, string>("reason", reason),
                new KeyValuePair<string, string>("cleanup_state", cleanupState));
        }
        return cleanupState;
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

        if (session != null)
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

        ValheimDevWorldCapture? candidateCapture = trackedWorld;
        if (candidateCapture != null
            && ValheimDevEligibility.CheckCapturedSession(candidateCapture, state) != "eligible")
        {
            string cleanupState = Revoke("authorization_world_changed");
            if (cleanupState == ValheimDevCleanupState.RestartRequired)
            {
                result = cleanupState;
                return false;
            }
            candidateCapture = null;
        }
        bool reusesTrackedWorld = candidateCapture != null;

        TcpListener candidate = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            candidate.Start(ValheimDevProtocol.MaximumQueueDepth);
            ValheimDevSessionIdentity candidateIdentity = CreateSessionIdentity();
            candidateIdentity.SessionId = Guid.NewGuid().ToString("N");
            candidateIdentity.AuthorizedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            int port = ((IPEndPoint)candidate.LocalEndpoint).Port;
            WriteDescriptor(candidateIdentity, port);

            candidateCapture ??= new ValheimDevWorldCapture
            {
                Network = state.Network!, Scene = state.Scene!, WorldId = state.WorldId
            };
            ValheimDevSession candidateSession = new ValheimDevSession(
                candidateIdentity,
                candidateCapture,
                candidate);
            trackedWorld = candidateCapture;
            session = candidateSession;
            Thread acceptThread = new Thread(() => AcceptLoop(candidateSession))
            {
                IsBackground = true,
                Name = "Valheim Dev listener"
            };
            acceptThread.Start();
            ValheimDevDiagnostics.Emit(
                "lab_authorized",
                new KeyValuePair<string, string>("lab_session_id", candidateIdentity.SessionId));
            result = "authorized";
            return true;
        }
        catch (Exception exception)
        {
            session = null;
            if (!reusesTrackedWorld) trackedWorld = null;
            candidate.Stop();
            DeleteDescriptor();
            result = "session_start_failed:" + ValheimDevDiagnostics.Flatten(exception.Message);
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
