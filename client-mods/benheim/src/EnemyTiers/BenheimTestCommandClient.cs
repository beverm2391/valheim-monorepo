using BenheimQoL.Infrastructure;
using BenheimQoL.Affinities;
using BenheimQoL.ValheimDev;
using System.Collections.Generic;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

internal static class BenheimTestCommandClient
{
    private const float ResultTimeoutSeconds = 5f;
    private static readonly Dictionary<string, float> PendingOperations = new();
    private static readonly List<string> ExpiredOperations = new();
    private static readonly List<Minimap.PinData> HengeOverlayPins = new();
    private static bool commandRegistered;
    private static ZRpc? registeredServerRpc;
    private static Minimap? hengeOverlayMinimap;
    private static string? pendingHengeOperationId;
    private static float pendingHengeRequestedAt;
    internal static void InitializeConsole()
    {
        if (commandRegistered)
        {
            return;
        }

        _ = new Terminal.ConsoleCommand(
            "bh",
            "selected Benheim admin test commands; run 'bh help'",
            Execute,
            isCheat: false,
            isNetwork: true);
        commandRegistered = true;
    }
    internal static void Update()
    {
        EnsureResultRpcRegistered();
        ExpireUnansweredRequests(Time.realtimeSinceStartup);
        ExpireHengeRequest(Time.realtimeSinceStartup);
    }
    internal static void Reset()
    {
        ClearHengeOverlay();
        registeredServerRpc = null;
        PendingOperations.Clear();
        ExpiredOperations.Clear();
        pendingHengeOperationId = null;
        pendingHengeRequestedAt = 0f;
    }
    private static object Execute(Terminal.ConsoleEventArgs args)
    {
        if (BoarTestCommandProtocol.IsHelpRequest(args.Args))
        {
            PrintHelp(args.Context);
            return true;
        }

        if (ValheimDevRuntime.TryHandleConsole(args.Args, args.Context)) return true;

        if (AffinityDebugCommand.TryExecute(args.Args, args.Context)) return true;

        if (HengeOverlayProtocol.TryParse(args.Args, out bool hengeEnabled))
        {
            return ExecuteHengeOverlay(hengeEnabled, args.Context);
        }

        if (!BoarTestCommandProtocol.TryParseSpawnBoar(args.Args, out int stars) ||
            !BoarTestCommandProtocol.TryResolveLevel(stars, out int level))
        {
            PrintHelp(args.Context);
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

    private static void PrintHelp(Terminal context)
    {
        context.AddString("Benheim test commands:");
        context.AddString($"  {BoarTestCommandProtocol.Usage}");
        context.AddString("  0 = unstarred, 1 = one star, 2 = two stars");
        context.AddString($"  {HengeOverlayProtocol.Usage}");
        context.AddString("  locally show or remove every native Yagluth-henge candidate");
        AffinityDebugCommand.PrintUsage(context);
        ValheimDevRuntime.PrintUsage(context);
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
        serverRpc.Register<string, string, string, ZPackage>(
            HengeOverlayProtocol.ResultRpc,
            OnHengeResult);
        registeredServerRpc = serverRpc;
        return true;
    }

    private static object ExecuteHengeOverlay(bool enabled, Terminal context)
    {
        if (!enabled)
        {
            pendingHengeOperationId = null;
            pendingHengeRequestedAt = 0f;
            int removed = ClearHengeOverlay();
            context.AddString($"Benheim removed {removed} local henge overlay pins.");
            return true;
        }

        ZRpc? serverRpc = ZNet.instance?.GetServerRPC();
        if (serverRpc == null || !EnsureResultRpcRegistered())
        {
            context.AddString("Benheim henge overlay unavailable: not connected to a compatible server.");
            return true;
        }

        string operationId = Diagnostics.NewOperationId();
        pendingHengeOperationId = operationId;
        pendingHengeRequestedAt = Time.realtimeSinceStartup;
        Diagnostics.Emit(
            DiagnosticEvent.Create("TestCommands", "henge_overlay_requested")
                .String("operation_id", operationId)
                .String("operation_phase", "start"));
        try
        {
            serverRpc.Invoke(HengeOverlayProtocol.RequestRpc, operationId);
        }
        catch
        {
            pendingHengeOperationId = null;
            pendingHengeRequestedAt = 0f;
            EmitHengeFailedResult(operationId, "request_send_failed");
            context.AddString("Benheim could not send the henge overlay request to the server.");
            return true;
        }

        context.AddString("Benheim requested the native henge plan from the server.");
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

    private static void OnHengeResult(
        ZRpc rpc,
        string operationId,
        string outcome,
        string reason,
        ZPackage payload)
    {
        if (!ReferenceEquals(rpc, ZNet.instance?.GetServerRPC()))
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("TestCommands", "henge_overlay_result_rejected")
                    .String("operation_id", operationId)
                    .String("operation_phase", "terminal")
                    .String("reason", "non_server_sender"));
            return;
        }

        if (!string.Equals(pendingHengeOperationId, operationId, System.StringComparison.Ordinal))
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("TestCommands", "henge_overlay_result_rejected")
                    .String("operation_id", operationId)
                    .String("operation_phase", "terminal")
                    .String("reason", "unknown_operation"));
            return;
        }

        pendingHengeOperationId = null;
        pendingHengeRequestedAt = 0f;
        if (outcome != "accepted")
        {
            EmitHengeFailedResult(operationId, reason);
            return;
        }

        if (!TryReadCoordinates(payload, out List<Vector3> coordinates))
        {
            EmitHengeFailedResult(operationId, "malformed_coordinate_payload");
            return;
        }

        if (!TryReplaceHengeOverlay(coordinates, out string overlayFailure))
        {
            EmitHengeFailedResult(operationId, overlayFailure);
            return;
        }

        Diagnostics.Emit(
            DiagnosticEvent.Create("TestCommands", "henge_overlay_result")
                .String("operation_id", operationId)
                .String("operation_phase", "terminal")
                .String("outcome", "accepted")
                .String("reason", reason)
                .Integer("coordinate_count", coordinates.Count));
        Console.instance?.Print($"Benheim marked {coordinates.Count} native henge locations.");
    }

    private static bool TryReadCoordinates(ZPackage payload, out List<Vector3> coordinates)
    {
        coordinates = new List<Vector3>();
        try
        {
            if (payload.Size() < sizeof(int))
            {
                return false;
            }

            int count = payload.ReadInt();
            int maxCoordinateCount = (payload.Size() - sizeof(int)) / (sizeof(float) * 3);
            if (count < 0 || count > maxCoordinateCount)
            {
                return false;
            }

            coordinates.Capacity = count;
            for (int index = 0; index < count; index++)
            {
                Vector3 coordinate = payload.ReadVector3();
                if (!IsFinite(coordinate))
                {
                    return false;
                }
                coordinates.Add(coordinate);
            }

            return payload.GetPos() == payload.Size();
        }
        catch
        {
            coordinates.Clear();
            return false;
        }
    }

    private static bool TryReplaceHengeOverlay(
        List<Vector3> coordinates,
        out string failure)
    {
        Minimap? minimap = Minimap.instance;
        Player? player = Player.m_localPlayer;
        if (minimap == null || player == null)
        {
            failure = "native_minimap_unavailable";
            return false;
        }

        ClearHengeOverlay();
        hengeOverlayMinimap = minimap;
        try
        {
            foreach (Vector3 coordinate in coordinates)
            {
                Minimap.PinData pin = minimap.AddPin(
                    coordinate,
                    Minimap.PinType.Icon3,
                    "",
                    save: false,
                    isChecked: false,
                    ownerID: 0L);
                HengeOverlayPins.Add(pin);
            }
        }
        catch
        {
            ClearHengeOverlay();
            failure = "native_pin_creation_failed";
            return false;
        }

        failure = "";
        return true;
    }

    private static int ClearHengeOverlay()
    {
        int removed = HengeOverlayPins.Count;
        if (hengeOverlayMinimap != null &&
            ReferenceEquals(hengeOverlayMinimap, Minimap.instance))
        {
            foreach (Minimap.PinData pin in HengeOverlayPins)
            {
                hengeOverlayMinimap.RemovePin(pin);
            }
        }

        HengeOverlayPins.Clear();
        hengeOverlayMinimap = null;
        return removed;
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

    private static void ExpireHengeRequest(float now)
    {
        if (pendingHengeOperationId == null ||
            now - pendingHengeRequestedAt < ResultTimeoutSeconds)
        {
            return;
        }

        string operationId = pendingHengeOperationId;
        pendingHengeOperationId = null;
        pendingHengeRequestedAt = 0f;
        EmitHengeFailedResult(operationId, "server_no_response");
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

    private static void EmitHengeFailedResult(string operationId, string reason)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("TestCommands", "henge_overlay_result")
                .String("operation_id", operationId)
                .String("operation_phase", "terminal")
                .String("outcome", "rejected")
                .String("reason", reason)
                .Integer("coordinate_count", 0));
        Console.instance?.Print($"Benheim server rejected the henge overlay request: {reason}.");
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
