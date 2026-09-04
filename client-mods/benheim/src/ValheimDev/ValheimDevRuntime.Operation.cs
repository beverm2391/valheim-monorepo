using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.ValheimDev;

internal static partial class ValheimDevRuntime
{
    private static void Process(ValheimDevPendingRequest pending)
    {
        ValheimDevRequest request = pending.Request;
        ValheimDevResponse response = ResponseFor(request);
        if (!authorized || capture == null)
        {
            response.Error = "not_authorized";
            pending.Complete(response.ToJson(request.Kind == "apply"));
            return;
        }
        if (request.Protocol != ValheimDevProtocol.ProtocolVersion)
        {
            response.Error = "protocol_mismatch";
            pending.Complete(response.ToJson(request.Kind == "apply"));
            return;
        }
        if (!ConstantTimeEquals(request.Token, identity.Token)
            || !string.Equals(request.Generation, identity.Generation, StringComparison.Ordinal))
        {
            response.Error = "authorization_mismatch";
            pending.Complete(response.ToJson(request.Kind == "apply"));
            return;
        }

        if (request.Kind == "status")
        {
            response.Ok = true;
            response.Authorized = true;
            pending.Complete(response.ToJson(apply: false));
            return;
        }

        response.OperationId = request.OperationId;
        response.EvidenceSelected = request.EvidenceEvents.Count > 0;
        response.EvidenceExhaustive = false;
        response.StartedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        if (restartRequired)
        {
            response.Error = ValheimDevCleanupState.RestartRequired;
            response.FinishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            pending.Complete(response.ToJson(apply: true));
            return;
        }

        string eligibility = ValheimDevEligibility.CheckOperation(capture, Snapshot());
        if (eligibility != "eligible")
        {
            response.Error = "ineligible:" + eligibility;
            response.FinishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            pending.Complete(response.ToJson(apply: true));
            if (ValheimDevEligibility.CheckCapturedSession(capture, Snapshot()) != "eligible")
            {
                Revoke("operation_drift:" + eligibility);
            }
            return;
        }

        if (!TryValidateArtifact(request, out byte[] assemblyBytes, out string artifactError))
        {
            response.Error = artifactError;
            response.FinishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            pending.Complete(response.ToJson(apply: true));
            return;
        }

        // Recheck immediately before Assembly.Load. The same check is repeated
        // before Run so neither static initialization nor experiment code can
        // cross a stale world authorization.
        eligibility = ValheimDevEligibility.CheckOperation(capture, Snapshot());
        if (eligibility != "eligible")
        {
            response.Error = "ineligible_before_load:" + eligibility;
            response.FinishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            pending.Complete(response.ToJson(apply: true));
            return;
        }

        ValheimDevActiveOperation operation = new ValheimDevActiveOperation
        {
            Pending = pending,
            Response = response,
            DeadlineUtc = DateTime.UtcNow.AddMilliseconds(request.EvidenceTimeoutMs)
        };
        foreach (string selector in request.EvidenceEvents) operation.Selectors.Add(selector);
        activeOperation = operation;
        lock (Gate) PendingEvidence.Clear();
        Diagnostics.SetValheimDevObserver(EnqueueEvidence);
        Diagnostics.Emit(
            DiagnosticEvent.Create("ValheimDev", "apply_started")
                .String("operation_id", request.OperationId)
                .String("source_sha256", request.SourceSha256)
                .String("assembly_sha256", request.AssemblySha256));

        eligibility = ValheimDevEligibility.CheckOperation(capture, Snapshot());
        if (eligibility != "eligible")
        {
            FinishActiveOperation("ineligible_before_run:" + eligibility, ok: false);
            return;
        }

        ValheimDevExecutionResult preparation = ValheimDevExperimentExecutor.Prepare(
            assemblyBytes,
            request.EntryType);
        if (!preparation.Ok || preparation.LoadedExperiment == null)
        {
            response.Exception = preparation.Exception;
            FinishActiveOperation(preparation.Error, ok: false);
            return;
        }
        operation.Experiment = preparation.LoadedExperiment;

        eligibility = ValheimDevEligibility.CheckOperation(capture, Snapshot());
        if (eligibility != "eligible")
        {
            FinishActiveOperation("ineligible_before_entrypoint:" + eligibility, ok: false);
            return;
        }

        ValheimDevExecutionResult execution = ValheimDevExperimentExecutor.Invoke(
            preparation.LoadedExperiment);
        response.Result = execution.Result;
        response.Exception = execution.Exception;
        if (!execution.Ok)
        {
            FinishActiveOperation(execution.Error, ok: false);
            return;
        }
        if (operation.Selectors.Count == 0 || request.EvidenceTimeoutMs == 0)
        {
            FinishActiveOperation(null, ok: true);
        }
    }

    private static string FinishActiveOperation(string? error, bool ok)
    {
        ValheimDevActiveOperation? operation = activeOperation;
        if (operation == null) return ValheimDevCleanupState.NotApplicable;

        // Events emitted synchronously by Run belong to this operation even
        // when its requested wait is zero. Close observation before Cleanup so
        // the experiment is active only for its evidence window.
        DrainEvidence();
        Diagnostics.SetValheimDevObserver(null);
        activeOperation = null;
        lock (Gate) PendingEvidence.Clear();
        operation.Response.CleanupState = CleanupExperiment(operation.Experiment);
        operation.Response.Ok = ok && error == null;
        operation.Response.Error = error;
        operation.Response.Authorized = authorized;
        operation.Response.FinishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        Diagnostics.Emit(
            DiagnosticEvent.Create("ValheimDev", "apply_finished")
                .String("operation_id", operation.Response.OperationId)
                .String("outcome", operation.Response.Ok ? "accepted" : "failed")
                .String("error", operation.Response.Error ?? string.Empty)
                .String("cleanup_state", operation.Response.CleanupState));
        operation.Pending.Complete(operation.Response.ToJson(apply: true));
        return operation.Response.CleanupState;
    }

    private static void EnqueueEvidence(DiagnosticEvent diagnosticEvent)
    {
        string selector = diagnosticEvent.Domain + ":" + diagnosticEvent.Name;
        lock (Gate)
        {
            ValheimDevActiveOperation? operation = activeOperation;
            if (operation != null
                && operation.Selectors.Contains(selector)
                && PendingEvidence.Count < ValheimDevProtocol.MaximumEvidenceEvents * 2)
            {
                PendingEvidence.Enqueue(diagnosticEvent);
            }
        }
    }

    private static void DrainEvidence()
    {
        ValheimDevActiveOperation? operation = activeOperation;
        if (operation == null) return;
        List<DiagnosticEvent> events = new List<DiagnosticEvent>();
        lock (Gate)
        {
            while (PendingEvidence.Count > 0) events.Add(PendingEvidence.Dequeue());
        }
        foreach (DiagnosticEvent diagnosticEvent in events)
        {
            string selector = diagnosticEvent.Domain + ":" + diagnosticEvent.Name;
            if (!operation.Selectors.Contains(selector)) continue;
            string json = diagnosticEvent.ToJsonLine();
            int bytes = Encoding.UTF8.GetByteCount(json);
            if (operation.Response.EvidenceEvents.Count >= ValheimDevProtocol.MaximumEvidenceEvents
                || operation.EvidenceBytes + bytes > ValheimDevProtocol.MaximumEvidenceBytes)
            {
                continue;
            }
            operation.Response.EvidenceEvents.Add(json);
            operation.EvidenceBytes += bytes;
            operation.ObservedSelectors.Add(selector);
        }
    }

    private static string CleanupExperiment(ValheimDevLoadedExperiment? experiment)
    {
        if (experiment == null) return ValheimDevCleanupState.NotApplicable;
        if (experiment.Cleanup == null)
        {
            restartRequired = true;
            return ValheimDevCleanupState.RestartRequired;
        }
        if (!ValheimDevExperimentExecutor.TryCleanup(experiment, out string? cleanupException))
        {
            restartRequired = true;
            Plugin.Log.LogWarning("Benheim Lab cleanup failed: " + Diagnostics.Flatten(cleanupException ?? "unknown"));
            return ValheimDevCleanupState.RestartRequired;
        }
        return ValheimDevCleanupState.Cleaned;
    }

    private static bool TryValidateArtifact(
        ValheimDevRequest request,
        out byte[] assemblyBytes,
        out string error)
    {
        assemblyBytes = Array.Empty<byte>();
        error = string.Empty;
        if (!string.Equals(request.EntryType, ValheimDevProtocol.ExpectedEntryType, StringComparison.Ordinal))
        {
            error = "entry_type_not_allowed";
            return false;
        }
        if (!IsSha256(request.SourceSha256) || !IsSha256(request.AssemblySha256))
        {
            error = "invalid_sha256";
            return false;
        }
        if (!string.Equals(Sha256(Encoding.UTF8.GetBytes(request.Source)), request.SourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            error = "source_sha256_mismatch";
            return false;
        }
        try
        {
            assemblyBytes = Convert.FromBase64String(request.AssemblyBase64);
        }
        catch (FormatException)
        {
            error = "assembly_base64_invalid";
            return false;
        }
        if (assemblyBytes.Length == 0 || assemblyBytes.Length > ValheimDevProtocol.MaximumAssemblyBytes)
        {
            error = "assembly_too_large";
            return false;
        }
        if (!string.Equals(Sha256(assemblyBytes), request.AssemblySha256, StringComparison.OrdinalIgnoreCase))
        {
            error = "assembly_sha256_mismatch";
            return false;
        }
        return true;
    }

    private static ValheimDevWorldState Snapshot()
    {
#if VALHEIM_DEV_TESTS
        if (snapshotOverride != null) return snapshotOverride();
#endif
        ValheimDevWorldState state = new ValheimDevWorldState
        {
            GameplayHooksHealthy = HealthReporting.GameplayActionsEnabled
        };
        try
        {
            ZNet? network = ZNet.instance;
            ZNetScene? scene = ZNetScene.instance;
            Player? player = Player.m_localPlayer;
            state.Network = network;
            state.Scene = scene;
            if (network != null)
            {
                state.WorldId = network.GetWorldUID();
                state.IsServer = network.IsServer();
                state.IsOpenServer = ZNet.IsOpenServer();
                state.IsDedicated = network.IsDedicated();
                state.PeerCount = network.GetPeers().Count;
                state.HasServerRpc = network.GetServerRPC() != null;
            }
            state.LocalPlayer = player;
            state.LocalPlayerIsAlive = player != null && player;
            state.LocalPlayerIsOwner = state.LocalPlayerIsAlive && player!.IsOwner();
        }
        catch
        {
            state.GameplayHooksHealthy = false;
        }
        return state;
    }

    private static ValheimDevResponse ResponseFor(ValheimDevRequest request)
    {
        return new ValheimDevResponse
        {
            Identity = identity,
            Authorized = authorized,
            OperationId = request.OperationId,
            EvidenceSelected = request.EvidenceEvents.Count > 0,
            EvidenceExhaustive = false
        };
    }
}
