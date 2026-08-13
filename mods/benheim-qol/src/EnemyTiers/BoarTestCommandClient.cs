using BenheimQoL.Infrastructure;
using System.Collections.Generic;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

internal static class BoarTestCommandClient
{
    private const float ResultTimeoutSeconds = 5f;
    private static readonly Dictionary<string, float> PendingOperations = new();
    private static readonly List<string> ExpiredOperations = new();
    private static bool commandRegistered;
    private static ZRpc? registeredServerRpc;

    internal static void InitializeConsole()
    {
        if (commandRegistered)
        {
            return;
        }

        _ = new Terminal.ConsoleCommand(
            "benheim",
            "selected Benheim admin test commands: spawn-boar 1|2",
            Execute,
            isCheat: false,
            isNetwork: true);
        commandRegistered = true;
    }

    internal static void Update()
    {
        EnsureResultRpcRegistered();
        ExpireUnansweredRequests(Time.realtimeSinceStartup);
    }

    internal static void Reset()
    {
        registeredServerRpc = null;
        PendingOperations.Clear();
        ExpiredOperations.Clear();
    }

    private static object Execute(Terminal.ConsoleEventArgs args)
    {
        if (!BoarTestCommandProtocol.TryParse(args.Args, out int stars) ||
            !BoarTestCommandProtocol.TryResolveLevel(stars, out int level))
        {
            args.Context.AddString($"Usage: {BoarTestCommandProtocol.Usage}");
            return true;
        }

        ZRpc? serverRpc = ZNet.instance?.GetServerRPC();
        if (serverRpc == null || !EnsureResultRpcRegistered())
        {
            args.Context.AddString("Benheim test command unavailable: not connected to a compatible server.");
            return true;
        }

        string operationId = Diagnostics.NewOperationId();
        Diagnostics.Emit(
            DiagnosticEvent.Create("EnemyTiers", "boar_test_spawn_requested")
                .String("operation_id", operationId)
                .String("operation_phase", "start")
                .Integer("stars", stars)
                .Integer("level", level));
        PendingOperations[operationId] = Time.realtimeSinceStartup;
        try
        {
            serverRpc.Invoke(BoarTestCommandProtocol.RequestRpc, operationId, stars);
        }
        catch
        {
            PendingOperations.Remove(operationId);
            EmitFailedResult(operationId, "request_send_failed");
            args.Context.AddString("Benheim could not send the Boar request to the server.");
            return true;
        }
        args.Context.AddString($"Benheim requested a {stars}-star Boar from the server.");
        return true;
    }

    private static bool EnsureResultRpcRegistered()
    {
        ZRpc? serverRpc = ZNet.instance?.GetServerRPC();
        if (serverRpc == null)
        {
            registeredServerRpc = null;
            return false;
        }

        if (ReferenceEquals(registeredServerRpc, serverRpc))
        {
            return true;
        }

        serverRpc.Register<string, string, string, int>(
            BoarTestCommandProtocol.ResultRpc,
            OnResult);
        registeredServerRpc = serverRpc;
        return true;
    }

    private static void OnResult(ZRpc rpc, string operationId, string outcome, string reason, int level)
    {
        if (!ReferenceEquals(rpc, ZNet.instance?.GetServerRPC()))
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("EnemyTiers", "boar_test_spawn_result_rejected")
                    .String("operation_id", operationId)
                    .String("operation_phase", "terminal")
                    .String("reason", "non_server_sender"));
            return;
        }

        if (!PendingOperations.Remove(operationId))
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("EnemyTiers", "boar_test_spawn_result_rejected")
                    .String("operation_id", operationId)
                    .String("operation_phase", "terminal")
                    .String("reason", "unknown_operation"));
            return;
        }

        Diagnostics.Emit(
            DiagnosticEvent.Create("EnemyTiers", "boar_test_spawn_result")
                .String("operation_id", operationId)
                .String("operation_phase", "terminal")
                .String("outcome", outcome)
                .String("reason", reason)
                .Integer("level", level));

        string message = outcome == "accepted"
            ? $"Benheim server spawned a {level - 1}-star Boar."
            : $"Benheim server rejected the Boar request: {reason}.";
        Console.instance?.Print(message);
    }

    private static void ExpireUnansweredRequests(float now)
    {
        if (PendingOperations.Count == 0)
        {
            return;
        }

        ExpiredOperations.Clear();
        foreach ((string operationId, float requestedAt) in PendingOperations)
        {
            if (now - requestedAt >= ResultTimeoutSeconds)
            {
                ExpiredOperations.Add(operationId);
            }
        }

        foreach (string operationId in ExpiredOperations)
        {
            PendingOperations.Remove(operationId);
            EmitFailedResult(operationId, "server_no_response");
        }
        ExpiredOperations.Clear();
    }

    private static void EmitFailedResult(string operationId, string reason)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("EnemyTiers", "boar_test_spawn_result")
                .String("operation_id", operationId)
                .String("operation_phase", "terminal")
                .String("outcome", "rejected")
                .String("reason", reason)
                .Integer("level", 0));
        Console.instance?.Print($"Benheim server rejected the Boar request: {reason}.");
    }
}
