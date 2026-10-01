using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BenheimInventoryProtocol;
using BenheimQoL.Infrastructure;
using BenheimQoL.InventoryFeature;
using UnityEngine;


internal static class TestSupport
{
    internal static string AcquireLeaseWithoutStartingBatch(ZRpc rpc)
    {
        if (!PutAwayLeaseClient.TryRequest(Time.unscaledTime, out string reason))
        {
            throw new InvalidOperationException($"Could not request fixture lease: {reason}");
        }

        string operationId = RequestedOperationId(rpc);
        rpc.Respond(operationId, PutAwayLeaseProtocol.Granted, "granted");
        ExpectTrue(PutAwayLeaseClient.TryTakeResult(out PutAwayLeaseResult? result), "fixture lease grant is ready");
        ExpectEqual(operationId, result!.OperationId, "fixture lease grant is correlated");
        return operationId;
    }

    internal static TestWorld BeginTest()
    {
        SetActiveOperation(null);
        SetPendingStart(null);
        PutAwayLeaseClient.Reset();
        InventoryTransactions.ResetForTest();
        Diagnostics.ResetForTest();
        QuickStackFeedback.Messages.Clear();
        TopLeftFeedbackHud.Messages.Clear();
        HarnessEvents.Events.Clear();
        NearbyContainerIndex.Containers = new List<Container>();
        SetRuntimeInitialized(false);
        Time.unscaledTime = 1f;

        ZRpc rpc = new ZRpc();
        ZNet.instance = new ZNet(rpc);
        Inventory playerInventory = new Inventory();
        Player player = new Player(playerInventory);
        Player.m_localPlayer = player;
        InventoryGui gui = new InventoryGui();
        return new TestWorld(rpc, playerInventory, player, gui);
    }

    internal static Container Chest(string name, ItemDrop.ItemData item)
    {
        Inventory inventory = new Inventory();
        inventory.Add(item);
        return new Container(name, inventory);
    }

    internal static ItemDrop.ItemData Item(string name, int stack = 1) => new ItemDrop.ItemData(name, stack);

    internal static string RequestedOperationId(ZRpc rpc)
    {
        RpcCall[] requests = rpc.Calls
            .Where(call => call.Name == PutAwayLeaseProtocol.RequestRpc)
            .ToArray();
        if (requests.Length == 0 || requests[requests.Length - 1].OperationId == null)
        {
            throw new InvalidOperationException("No Put Away request operation ID was sent.");
        }

        return requests[requests.Length - 1].OperationId!;
    }

    internal static List<RpcCall> ReleaseCalls(ZRpc rpc) =>
        rpc.Calls.Where(call => call.Name == PutAwayLeaseProtocol.ReleaseRpc).ToList();

    internal static void InvokeBeforeNetworkDestroy()
    {
        MethodInfo method = typeof(InventoryTransactionRuntime).GetMethod(
            "BeforeNetworkDestroy",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find the network teardown patch entrypoint.");
        method.Invoke(null, null);
    }

    internal static void InvokeBeforeNetworkShutdown()
    {
        MethodInfo method = typeof(InventoryTransactionRuntime).GetMethod(
            "BeforeNetworkShutdown",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find the network shutdown patch entrypoint.");
        method.Invoke(null, null);
    }

    internal static void SetRuntimeInitialized(bool value) =>
        typeof(InventoryTransactionRuntime).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, value);

    internal static void SetActiveOperation(QuickStackOperation? operation) =>
        typeof(QuickStack).GetField("activeOperation", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, operation);

    internal static QuickStackOperation? GetActiveOperation() =>
        (QuickStackOperation?)typeof(QuickStack).GetField("activeOperation", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null);

    internal static void SetPendingStart(QuickStackStartRequest? start) =>
        typeof(QuickStack).GetField("pendingStart", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, start);

    internal static void ExpectField(
        InventoryTransactionDiagnosticEvent diagnosticEvent,
        string name,
        object expected,
        string scenario)
    {
        InventoryTransactionDiagnosticField field = diagnosticEvent.Fields.Single(value => value.Name == name);
        object actual = field.Kind switch
        {
            InventoryTransactionDiagnosticValueKind.String => field.Text!,
            InventoryTransactionDiagnosticValueKind.Integer => field.Integer,
            _ => throw new InvalidOperationException($"Unexpected diagnostic field kind for {name}.")
        };
        ExpectEqual(expected, actual, scenario);
    }

    internal static void ExpectTrue(bool condition, string scenario)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"{scenario}: expected true.");
        }
    }

    internal static void ExpectFalse(bool condition, string scenario) => ExpectTrue(!condition, scenario);

    internal static void ExpectEqual<T>(T expected, T actual, string scenario)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{scenario}: expected '{expected}', got '{actual}'.");
        }
    }

    internal static void ExpectNotEqual<T>(T unexpected, T actual, string scenario)
    {
        if (EqualityComparer<T>.Default.Equals(unexpected, actual))
        {
            throw new InvalidOperationException($"{scenario}: both values were '{actual}'.");
        }
    }

    internal static void ExpectSame(object expected, object? actual, string scenario)
    {
        if (!ReferenceEquals(expected, actual))
        {
            throw new InvalidOperationException($"{scenario}: object identity changed.");
        }
    }

}

internal sealed class TestWorld
{
    internal TestWorld(ZRpc rpc, Inventory playerInventory, Player player, InventoryGui gui)
    {
        Rpc = rpc;
        PlayerInventory = playerInventory;
        Player = player;
        Gui = gui;
    }

    internal ZRpc Rpc { get; }
    internal Inventory PlayerInventory { get; }
    internal Player Player { get; }
    internal InventoryGui Gui { get; }
}
