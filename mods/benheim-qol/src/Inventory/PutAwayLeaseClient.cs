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
    private static string? pendingOperationId;
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
        if (serverRpc == null || !EnsureResultRpcRegistered(serverRpc))
        {
            reason = "compatible_server_unavailable";
            return false;
        }

        string operationId = Diagnostics.NewOperationId();
        pendingOperationId = operationId;
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

    internal static void Update(float now)
    {
        ZRpc? serverRpc = ZNet.instance?.GetServerRPC();
        if (serverRpc != null)
        {
            EnsureResultRpcRegistered(serverRpc);
        }
        else
        {
            registeredServerRpc = null;
        }

        if (pendingOperationId == null || now - requestedAt < ResultWaitSeconds)
        {
            return;
        }

        string operationId = pendingOperationId;
        pendingOperationId = null;
        TrySendRelease(operationId, "result_timeout");
        completedResult = new PutAwayLeaseResult(operationId, false, "server_no_response");
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
        completedResult = null;
        if (operationId != null)
        {
            TrySendRelease(operationId, "client_reset");
        }

        registeredServerRpc = null;
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

        pendingOperationId = null;
        bool granted = outcome == PutAwayLeaseProtocol.Granted;
        if (granted)
        {
            heldOperationId = operationId;
        }

        completedResult = new PutAwayLeaseResult(operationId, granted, reason);
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
    internal PutAwayLeaseResult(string operationId, bool granted, string reason)
    {
        OperationId = operationId;
        Granted = granted;
        Reason = reason;
    }

    internal string OperationId { get; }
    internal bool Granted { get; }
    internal string Reason { get; }
}
