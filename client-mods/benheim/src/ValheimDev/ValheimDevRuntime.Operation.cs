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
            Complete(pending, response, "not_authorized");
            return;
        }
        if (request.Protocol != ValheimDevProtocol.ProtocolVersion)
        {
            Complete(pending, response, "protocol_mismatch");
            return;
        }
        if (!ConstantTimeEquals(request.Token, identity.Token)
            || !string.Equals(request.Generation, identity.Generation, StringComparison.Ordinal))
        {
            Complete(pending, response, "authorization_mismatch");
            return;
        }
        if (request.Kind == "status")
        {
            response.Ok = true;
            response.Authorized = true;
            SnapshotActiveChanges(response);
            pending.Complete(response.ToJson(includeOperation: false));
            return;
        }

        response.StartedUtc = UtcNow();
        if (restartRequired && request.Kind != "inspect")
        {
            Complete(pending, response, ValheimDevCleanupState.RestartRequired);
            return;
        }
        if (!CheckEligibility(response, pending, "operation")) return;
        if (request.Kind == "remove_change")
        {
            if (!MatchesExpectedChangeState(request.ChangeId, request.ExpectedOperationId))
            {
                Complete(pending, response, "stale_change_state");
                return;
            }
            RemoveChange(pending, response, request.ChangeId);
            return;
        }
        if (!TryValidateArtifact(request, out byte[] assemblyBytes, out string artifactError))
        {
            Complete(pending, response, artifactError);
            return;
        }
        if (!CheckEligibility(response, pending, "before_load")) return;

        bool managed = request.Kind == "install_change";
        ValheimDevExecutionResult preparation = ValheimDevCodeExecutor.Prepare(
            assemblyBytes,
            request.EntryType,
            requireCleanup: managed);
        if (!preparation.Ok || preparation.LoadedCode == null)
        {
            response.Exception = preparation.Exception;
            Complete(pending, response, preparation.Error);
            return;
        }
        if (!CheckEligibility(response, pending, "before_entrypoint")) return;

        if (managed && !MatchesExpectedChangeState(request.ChangeId, request.ExpectedOperationId))
        {
            Complete(pending, response, "stale_change_state");
            return;
        }

        BeginOperation(pending, response, request);
        if (managed) InstallChange(preparation.LoadedCode, request);
        else RunInspection(preparation.LoadedCode, request);
    }

    private static void RunInspection(ValheimDevLoadedCode code, ValheimDevRequest request)
    {
        ValheimDevExecutionResult execution = ValheimDevCodeExecutor.Invoke(code);
        activeOperation!.Response.Result = execution.Result;
        activeOperation.Response.Exception = execution.Exception;
        if (!execution.Ok)
        {
            FinishActiveOperation(execution.Error, ok: false, ValheimDevCleanupState.NotApplicable);
            return;
        }
        FinishOrObserve(request, ValheimDevCleanupState.NotApplicable);
    }

    private static void BeginOperation(
        ValheimDevPendingRequest pending,
        ValheimDevResponse response,
        ValheimDevRequest request)
    {
        ValheimDevActiveOperation operation = new ValheimDevActiveOperation
        {
            Pending = pending,
            Response = response,
            DeadlineUtc = DateTime.UtcNow.AddMilliseconds(request.EvidenceTimeoutMs),
            EvidenceBytes = 2
        };
        foreach (string selector in request.EvidenceEvents) operation.Selectors.Add(selector);
        lock (Gate)
        {
            activeOperation = operation;
            PendingEvidence.Clear();
        }
        Diagnostics.SetValheimDevObserver(diagnosticEvent => EnqueueEvidence(operation, diagnosticEvent));
        Diagnostics.Emit(
            DiagnosticEvent.Create("ValheimDev", "operation_started")
                .String("action", request.Kind)
                .String("operation_id", request.OperationId)
                .String("change_id", request.ChangeId)
                .String("source_sha256", request.SourceSha256)
                .String("assembly_sha256", request.AssemblySha256));
    }

    private static void FinishOrObserve(ValheimDevRequest request, string cleanupState)
    {
        activeOperation!.CompletionCleanupState = cleanupState;
        if (activeOperation.Selectors.Count == 0 || request.EvidenceTimeoutMs == 0)
        {
            FinishActiveOperation(null, ok: true, cleanupState);
        }
    }

    private static string FinishActiveOperation(string? error, bool ok, string cleanupState)
    {
        ValheimDevActiveOperation? operation;
        lock (Gate)
        {
            operation = activeOperation;
            if (operation == null) return ValheimDevCleanupState.NotApplicable;
            activeOperation = null;
        }

        Diagnostics.SetValheimDevObserver(null);
        DrainEvidence(operation);
        operation.Response.CleanupState = cleanupState;
        operation.Response.Ok = ok && error == null;
        operation.Response.Error = error;
        operation.Response.Authorized = authorized;
        operation.Response.RestartRequired = restartRequired;
        operation.Response.FinishedUtc = UtcNow();
        SnapshotActiveChanges(operation.Response);
        Diagnostics.Emit(
            DiagnosticEvent.Create("ValheimDev", "operation_finished")
                .String("action", operation.Response.Action)
                .String("operation_id", operation.Response.OperationId)
                .String("outcome", operation.Response.Ok ? "accepted" : "failed")
                .String("error", operation.Response.Error ?? string.Empty)
                .String("cleanup_state", cleanupState));
        operation.Pending.Complete(operation.Response.ToJson(includeOperation: true));
        return cleanupState;
    }

    private static void EnqueueEvidence(
        ValheimDevActiveOperation expectedOperation,
        DiagnosticEvent diagnosticEvent)
    {
        string selector = diagnosticEvent.Domain + ":" + diagnosticEvent.Name;
        lock (Gate)
        {
            ValheimDevActiveOperation? operation = activeOperation;
            if (ReferenceEquals(operation, expectedOperation)
                && operation.Selectors.Contains(selector))
            {
                if (PendingEvidence.Count < ValheimDevProtocol.MaximumEvidenceEvents * 2)
                {
                    PendingEvidence.Enqueue(diagnosticEvent);
                }
                else
                {
                    operation.Response.EvidenceTruncated = true;
                    operation.Response.DroppedEvidenceEvents++;
                }
            }
        }
    }

    private static void DrainEvidence(ValheimDevActiveOperation operation)
    {
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
            int bytes = ValheimDevJson.EncodedStringUtf8ByteCount(json)
                + (operation.Response.EvidenceEvents.Count == 0 ? 0 : 1);
            if (operation.Response.EvidenceEvents.Count >= ValheimDevProtocol.MaximumEvidenceEvents
                || operation.EvidenceBytes + bytes > ValheimDevProtocol.MaximumEvidenceBytes)
            {
                operation.Response.EvidenceTruncated = true;
                operation.Response.DroppedEvidenceEvents++;
                continue;
            }
            operation.Response.EvidenceEvents.Add(json);
            operation.EvidenceBytes += bytes;
            operation.ObservedSelectors.Add(selector);
        }
    }

    private static bool TryValidateArtifact(
        ValheimDevRequest request,
        out byte[] assemblyBytes,
        out string error)
    {
        assemblyBytes = Array.Empty<byte>();
        error = string.Empty;
        string expectedEntryType = request.Kind == "inspect"
            ? ValheimDevProtocol.InspectionEntryType
            : ValheimDevProtocol.ChangeEntryType;
        if (!string.Equals(request.EntryType, expectedEntryType, StringComparison.Ordinal))
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
        try { assemblyBytes = Convert.FromBase64String(request.AssemblyBase64); }
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

    private static bool CheckEligibility(
        ValheimDevResponse response,
        ValheimDevPendingRequest pending,
        string boundary)
    {
        ValheimDevWorldState current = Snapshot();
        string eligibility = ValheimDevEligibility.CheckOperation(capture!, current);
        if (eligibility == "eligible") return true;
        if (ValheimDevEligibility.CheckCapturedSession(capture!, current) != "eligible")
        {
            Revoke("operation_drift:" + eligibility);
            response.Authorized = false;
            response.RestartRequired = restartRequired;
        }
        Complete(pending, response, "ineligible_" + boundary + ":" + eligibility);
        return false;
    }

    private static void Complete(
        ValheimDevPendingRequest pending,
        ValheimDevResponse response,
        string error)
    {
        if (pending.Request.Kind == "install_change"
            && error != "stale_change_state"
            && HasManagedChange(pending.Request.ChangeId))
        {
            response.PreviousChangePreserved = true;
        }
        response.Error = error;
        response.RestartRequired = restartRequired;
        response.FinishedUtc = UtcNow();
        SnapshotActiveChanges(response);
        pending.Complete(response.ToJson(includeOperation: pending.Request.Kind != "status"));
    }

    private static string UtcNow() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

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
        catch { state.GameplayHooksHealthy = false; }
        return state;
    }

    private static ValheimDevResponse ResponseFor(ValheimDevRequest request)
    {
        return new ValheimDevResponse
        {
            Identity = identity,
            Authorized = authorized,
            RestartRequired = restartRequired,
            Action = request.Kind,
            OperationId = request.OperationId,
            ChangeId = request.ChangeId,
            EvidenceSelected = request.EvidenceEvents.Count > 0,
            EvidenceExhaustive = false
        };
    }
}
