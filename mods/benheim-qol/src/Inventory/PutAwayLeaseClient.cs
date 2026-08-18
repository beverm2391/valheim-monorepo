using BenheimQoL.Infrastructure;
using System;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

/// <summary>
/// Owns the one direct-server lease that prevents compatible clients from
/// entering Put Away scanning and reservation concurrently.
/// </summary>
internal static class PutAwayLeaseClient
{
    internal const float ResultWaitSeconds = 5f;

    private static ZRpc? registeredServerRpc;
    private static ZRpc? readinessSentServerRpc;
    private static string? pendingOperationId;
    private static bool pendingValidation;
    private static string? heldOperationId;
    private static float requestedAt;
    private static PutAwayLeaseResult? completedResult;

    internal static bool IsPendingOrHeld => pendingOperationId != null || heldOperationId != null;

    internal static bool TryRequest(float now, out string reason)
    {
        reason = string.Empty;
        if (IsPendingOrHeld)
        {
            reason = "local_operation_in_progress";
            return false;
        }

        ZRpc? serverRpc = ZNet.instance?.GetServerRPC();
        if (serverRpc == null
            || !EnsureResultRpcRegistered(serverRpc)
            || !EnsurePeerReadinessSent(serverRpc))
        {
            reason = "compatible_server_unavailable";
            return false;
        }

        string operationId = Diagnostics.NewOperationId();
        pendingOperationId = operationId;
        pendingValidation = false;
        requestedAt = now;
        Diagnostics.Emit(
            DiagnosticEvent.Create("Inventory", "quick_stack_lease_requested")
                .String("operation_id", operationId)
                .String("operation_phase", "lease_request"));
        try
        {
            serverRpc.Invoke(PutAwayLeaseProtocol.RequestRpc, operationId);
            return true;
        }
        catch (Exception exception)
        {
            pendingOperationId = null;
            reason = $"request_send_failed_{exception.GetType().Name}";
            EmitResult(operationId, PutAwayLeaseProtocol.Rejected, reason);
            return false;
        }
    }

    internal static bool TryValidate(string operationId, float now, out string reason)
    {
        reason = string.Empty;
        if (heldOperationId != operationId || pendingOperationId != null)
        {
            reason = "lease_not_held";
            return false;
        }

        ZRpc? serverRpc = ZNet.instance?.GetServerRPC();
        if (serverRpc == null || !serverRpc.IsConnected())
        {
            reason = "compatible_server_unavailable";
            return false;
        }

        pendingOperationId = operationId;
        pendingValidation = true;
        requestedAt = now;
        try
        {
            serverRpc.Invoke(PutAwayLeaseProtocol.RequestRpc, operationId);
            return true;
        }
        catch (Exception exception)
        {
            pendingOperationId = null;
            pendingValidation = false;
            reason = $"validation_send_failed_{exception.GetType().Name}";
            return false;
        }
    }

    internal static void Update(float now)
    {
        ZRpc? serverRpc = ZNet.instance?.GetServerRPC();
        if (serverRpc != null)
        {
            EnsureResultRpcRegistered(serverRpc);
            EnsurePeerReadinessSent(serverRpc);
        }
        else
        {
            registeredServerRpc = null;
            readinessSentServerRpc = null;
        }

        if (pendingOperationId == null || now - requestedAt < ResultWaitSeconds)
        {
            return;
        }

        string operationId = pendingOperationId;
        bool wasValidation = pendingValidation;
        pendingOperationId = null;
        pendingValidation = false;
        if (heldOperationId == operationId)
        {
            heldOperationId = null;
        }
        TrySendRelease(operationId, "result_timeout");
        completedResult = new PutAwayLeaseResult(
            operationId,
            false,
            "server_no_response",
            wasValidation);
        EmitResult(operationId, PutAwayLeaseProtocol.Rejected, "server_no_response");
    }

    internal static bool TryTakeResult(out PutAwayLeaseResult? result)
    {
        result = completedResult;
        completedResult = null;
        return result != null;
    }

    internal static void Release(string reason)
    {
        string? operationId = heldOperationId;
        heldOperationId = null;
        if (operationId != null)
        {
            TrySendRelease(operationId, reason);
        }
    }

    internal static void Reset()
    {
        string? operationId = heldOperationId ?? pendingOperationId;
        heldOperationId = null;
        pendingOperationId = null;
        pendingValidation = false;
        completedResult = null;
        if (operationId != null)
        {
            TrySendRelease(operationId, "client_reset");
        }

        registeredServerRpc = null;
        readinessSentServerRpc = null;
    }

    private static bool EnsureResultRpcRegistered(ZRpc serverRpc)
    {
        if (ReferenceEquals(registeredServerRpc, serverRpc))
        {
            return true;
        }

        try
        {
            serverRpc.Register<string, string, string>(PutAwayLeaseProtocol.ResultRpc, OnResult);
            registeredServerRpc = serverRpc;
            return true;
        }
        catch
        {
            registeredServerRpc = null;
            return false;
        }
    }

    private static bool EnsurePeerReadinessSent(ZRpc serverRpc)
    {
        if (ReferenceEquals(readinessSentServerRpc, serverRpc))
        {
            if (serverRpc.IsConnected())
            {
                return true;
            }

            readinessSentServerRpc = null;
            return false;
        }

        if (!serverRpc.IsConnected())
        {
            readinessSentServerRpc = null;
            return false;
        }

        try
        {
            serverRpc.Invoke(
                PutAwayLeaseProtocol.PeerReadyRpc,
                PutAwayLeaseProtocol.Generation);
            readinessSentServerRpc = serverRpc;
            Diagnostics.Emit(
                DiagnosticEvent.Create("Inventory", "put_away_peer_ready_sent")
                    .String("operation_phase", "peer_readiness")
                    .Integer("protocol_generation", PutAwayLeaseProtocol.Generation));
            return true;
        }
        catch
        {
            readinessSentServerRpc = null;
            return false;
        }
    }

    private static void OnResult(ZRpc rpc, string operationId, string outcome, string reason)
    {
        if (!ReferenceEquals(rpc, ZNet.instance?.GetServerRPC()))
        {
            EmitRejectedResult(operationId, "non_server_sender");
            return;
        }

        if (pendingOperationId != operationId)
        {
            EmitRejectedResult(operationId, "unknown_operation");
            return;
        }

        bool wasValidation = pendingValidation;
        pendingOperationId = null;
        pendingValidation = false;
        bool granted = outcome == PutAwayLeaseProtocol.Granted;
        if (granted)
        {
            if (!wasValidation)
            {
                heldOperationId = operationId;
            }
        }

        completedResult = new PutAwayLeaseResult(
            operationId,
            granted,
            reason,
            wasValidation);
        EmitResult(operationId, outcome, reason);
    }

    private static void TrySendRelease(string operationId, string reason)
    {
        bool sent = false;
        try
        {
            ZRpc? serverRpc = ZNet.instance?.GetServerRPC();
            if (serverRpc != null)
            {
                serverRpc.Invoke(PutAwayLeaseProtocol.ReleaseRpc, operationId);
                sent = true;
            }
        }
        catch
        {
            // Safe failure: the server retains the lease until peer disconnect.
        }

        Diagnostics.Emit(
            DiagnosticEvent.Create("Inventory", "quick_stack_lease_released")
                .String("operation_id", operationId)
                .String("operation_phase", "lease_release")
                .String("reason", reason)
                .Boolean("sent", sent));
    }

    private static void EmitResult(string operationId, string outcome, string reason)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("Inventory", "quick_stack_lease_result")
                .String("operation_id", operationId)
                .String("operation_phase", "lease_result")
                .String("outcome", outcome)
                .String("reason", reason));
    }

    private static void EmitRejectedResult(string operationId, string reason)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("Inventory", "quick_stack_lease_result_rejected")
                .String("operation_id", operationId)
                .String("operation_phase", "lease_result")
                .String("reason", reason));
    }
}

internal sealed class PutAwayLeaseResult
{
    internal PutAwayLeaseResult(
        string operationId,
        bool granted,
        string reason,
        bool isValidation)
    {
        OperationId = operationId;
        Granted = granted;
        Reason = reason;
        IsValidation = isValidation;
    }

    internal string OperationId { get; }
    internal bool Granted { get; }
    internal string Reason { get; }
    internal bool IsValidation { get; }
}
