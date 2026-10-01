using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BenheimInventoryProtocol;
using BenheimQoL.Infrastructure;
using BenheimQoL.InventoryFeature;
using UnityEngine;

using static TestSupport;

try
{
    if (args.Contains("--unsafe-control", StringComparer.Ordinal))
    {
        ReproduceLegacyScanLeaseStranding();
        Console.WriteLine("unsafe control reproduced: scan exception stranded the lease and blocked retry");
        return 0;
    }

    InitialScanFailureReleasesAndAllowsRetry();
    LaterDependencyFailureDrainsBeforeRelease();
    UnavailableContainersAreSkippedAlongsideHealthyTargets();
    AllUnavailableContainersFinishWithoutReservation();
    NetworkShutdownAndDestroyReleaseAnOrphanedLeaseOnce();
    DirectDestroyReleasesAnOrphanedLease();
    NetworkShutdownAndDestroyPreserveAnUnsettledBatch();
    Console.WriteLine("quick-stack lifecycle regression checks passed");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

static void ReproduceLegacyScanLeaseStranding()
{
    TestWorld world = BeginTest();
    world.PlayerInventory.Add(Item("Iron"));
    NearbyContainerIndex.Containers = new List<Container> { Chest("Iron chest", Item("Iron", 8)) };
    world.PlayerInventory.ThrowOnGridRead = 1;

    QuickStack.Run(world.Player, world.Gui, currentContainer: null);
    string operationId = RequestedOperationId(world.Rpc);
    world.Rpc.Respond(operationId, PutAwayLeaseProtocol.Granted, "granted");
    bool scanEscaped = false;
    try
    {
        QuickStack.Update();
    }
    catch (InvalidOperationException exception) when (exception.Message.Contains("scan failure", StringComparison.Ordinal))
    {
        scanEscaped = true;
    }

    ExpectTrue(scanEscaped, "baseline initial scan exception escapes the lifecycle");
    ExpectTrue(PutAwayLeaseClient.IsPendingOrHeld, "baseline leaves the granted lease held");
    ExpectEqual(0, InventoryTransactions.Terminals.Count, "baseline emits no terminal for the failed scan");
    ExpectEqual(0, ReleaseCalls(world.Rpc).Count, "baseline sends no lease release");
    ExpectEqual(0, QuickStackFeedback.Messages.Count, "baseline shows no scan failure feedback");

    QuickStack.Run(world.Player, world.Gui, currentContainer: null);
    ExpectEqual(1, world.Rpc.Calls.Count(call => call.Name == PutAwayLeaseProtocol.RequestRpc), "baseline retry sends no new lease request");
    ExpectTrue(
        TopLeftFeedbackHud.Messages.Any(message => message.Contains("already in progress", StringComparison.Ordinal)),
        "baseline retry is rejected as already in progress");
}

static void InitialScanFailureReleasesAndAllowsRetry()
{
    TestWorld world = BeginTest();
    ItemDrop.ItemData iron = Item("Iron");
    world.PlayerInventory.Add(iron);
    NearbyContainerIndex.Containers = new List<Container>
    {
        Chest("Iron chest", Item("Iron", 8))
    };
    world.PlayerInventory.ThrowOnGridRead = 1;
    Diagnostics.ThrowOnEmitName = "quick_stack_lease_released";

    QuickStack.Run(world.Player, world.Gui, currentContainer: null);
    string operationId = RequestedOperationId(world.Rpc);
    world.Rpc.Respond(operationId, PutAwayLeaseProtocol.Granted, "granted");
    QuickStack.Update();

    ExpectEqual(1, InventoryTransactions.Terminals.Count, "initial scan produces one terminal");
    BatchTerminal failed = InventoryTransactions.Terminals[0];
    ExpectEqual(operationId, failed.OperationId, "scan terminal keeps the lease operation ID");
    ExpectEqual("cancelled", failed.Status, "initial scan failure is visible as cancellation");
    ExpectEqual("container_scan_failed", failed.Reason, "initial scan failure has a precise reason");
    ExpectEqual(1, ReleaseCalls(world.Rpc).Count, "initial scan failure releases exactly once");
    ExpectEqual(operationId, ReleaseCalls(world.Rpc)[0].OperationId, "release uses the terminal operation ID");
    ExpectTrue(
        QuickStackFeedback.Messages.Any(message => message.Contains("Put Away stopped", StringComparison.Ordinal)),
        "initial scan failure shows player-facing feedback");
    ExpectTrue(
        InventoryTransactions.DiagnosticEvents.Any(diagnosticEvent => diagnosticEvent.Name == "put_away_scan_failed"),
        "initial scan failure emits typed evidence");
    ExpectFalse(PutAwayLeaseClient.IsPendingOrHeld, "initial scan failure clears the client lease");

    // A broken release diagnostic must not undo the RPC release, terminal, or
    // the next Put Away request. This also exercises the real lease client.
    Diagnostics.ThrowOnEmitName = null;
    world.PlayerInventory.ThrowOnGridRead = null;
    NearbyContainerIndex.Containers = new List<Container>();
    QuickStack.Run(world.Player, world.Gui, currentContainer: null);
    string retryOperationId = RequestedOperationId(world.Rpc);
    ExpectNotEqual(operationId, retryOperationId, "immediate retry receives a new operation ID");
    world.Rpc.Respond(retryOperationId, PutAwayLeaseProtocol.Granted, "granted");
    QuickStack.Update();

    ExpectEqual(2, InventoryTransactions.StartedOperationIds.Count, "retry starts a second batch");
    ExpectEqual(2, InventoryTransactions.Terminals.Count, "retry also reaches a terminal");
    ExpectEqual(retryOperationId, InventoryTransactions.Terminals[1].OperationId, "retry terminal is correlated");
    ExpectEqual(2, ReleaseCalls(world.Rpc).Count, "each operation releases once");
    ExpectFalse(PutAwayLeaseClient.IsPendingOrHeld, "retry completes without a stale lease");
    InvokeBeforeNetworkShutdown();
    InvokeBeforeNetworkDestroy();
    ExpectEqual(2, InventoryTransactions.Terminals.Count, "later teardown does not duplicate batch terminals");
    ExpectEqual(2, ReleaseCalls(world.Rpc).Count, "later teardown does not duplicate lease releases");
}

static void LaterDependencyFailureDrainsBeforeRelease()
{
    TestWorld world = BeginTest();
    ItemDrop.ItemData iron = Item("Iron", 2);
    ItemDrop.ItemData copper = Item("Copper", 2);
    ItemDrop.ItemData bronze = Item("Bronze", 2);
    world.PlayerInventory.Add(iron);
    world.PlayerInventory.Add(copper);
    world.PlayerInventory.Add(bronze);

    // The unavailable and destroyed objects are present in the same discovery
    // result as healthy eligible chests, matching the recently placed-chest
    // failure shape without discarding the healthy work.
    Container missing = new Container("Not ready", inventory: null);
    Container destroyed = Chest("Destroyed", Item("Iron", 4));
    destroyed.Destroyed = true;
    Container first = Chest("Iron chest", Item("Iron", 8));
    Container second = Chest("Copper chest", Item("Copper", 8));
    Inventory lastInventory = new Inventory();
    lastInventory.Add(Item("Bronze", 8));
    lastInventory.ThrowOnContentsRead = 2;
    Container last = new Container("Bronze chest", lastInventory);
    NearbyContainerIndex.Containers = new List<Container> { missing, destroyed, first, second, last };

    QuickStack.Run(world.Player, world.Gui, currentContainer: null);
    string operationId = RequestedOperationId(world.Rpc);
    world.Rpc.Respond(operationId, PutAwayLeaseProtocol.Granted, "granted");
    QuickStack.Update(); // Initial lease grant; requests validation for the Iron chest.
    world.Rpc.Respond(operationId, PutAwayLeaseProtocol.Granted, "validated");
    QuickStack.Update(); // Starts the Iron deposit, then requests Copper validation.

    ExpectEqual(1, InventoryTransactions.BeginDepositCount, "the first authoritative deposit is in flight");
    ExpectEqual(0, InventoryTransactions.Terminals.Count, "the active batch has not completed");
    ExpectEqual(0, ReleaseCalls(world.Rpc).Count, "the lease stays held with a deposit in flight");
    ExpectTrue(PutAwayLeaseClient.IsPendingOrHeld, "the owner-authoritative lease remains held");

    // The first dependency scan reads the Bronze chest once. The second
    // validation reads it again and throws before reserving Copper.
    world.Rpc.Respond(operationId, PutAwayLeaseProtocol.Granted, "validated");
    QuickStack.Update();
    ExpectEqual(1, InventoryTransactions.BeginDepositCount, "the failed pre-reservation scan starts no second deposit");
    ExpectEqual(0, InventoryTransactions.Terminals.Count, "cancellation waits for the in-flight result");
    ExpectEqual(0, ReleaseCalls(world.Rpc).Count, "cancellation does not release before settlement");
    ExpectTrue(
        TopLeftFeedbackHud.Messages.Any(message => message.Contains("couldn't scan nearby containers", StringComparison.Ordinal)),
        "later scan failure tells the player Put Away stopped");
    InventoryTransactionDiagnosticEvent failure = InventoryTransactions.DiagnosticEvents
        .Single(diagnosticEvent => diagnosticEvent.Name == "put_away_scan_failed");
    ExpectField(failure, "operation_id", operationId, "later scan evidence is correlated");
    ExpectField(failure, "in_flight", 1L, "later scan evidence reports the pending deposit");

    InventoryTransactions.SettleNextDeposit();
    ExpectEqual(1, InventoryTransactions.Terminals.Count, "settlement drains the cancelled batch exactly once");
    ExpectEqual(operationId, InventoryTransactions.Terminals[0].OperationId, "drained terminal keeps its operation ID");
    ExpectEqual("cancelled", InventoryTransactions.Terminals[0].Status, "failed scheduling ends as cancellation");
    ExpectEqual("container_dependency_scan_failed", InventoryTransactions.Terminals[0].Reason, "dependency failure is identified");
    ExpectEqual(1, ReleaseCalls(world.Rpc).Count, "the lease releases once after settlement");
    ExpectEqual(operationId, ReleaseCalls(world.Rpc)[0].OperationId, "drained release uses the batch ID");
    ExpectTrue(
        HarnessEvents.Events.IndexOf("deposit-settled") < HarnessEvents.Events.IndexOf($"rpc:{PutAwayLeaseProtocol.ReleaseRpc}"),
        "settlement precedes lease release");
}

static void UnavailableContainersAreSkippedAlongsideHealthyTargets()
{
    Inventory playerInventory = new Inventory();
    ItemDrop.ItemData iron = Item("Iron");
    playerInventory.Add(iron);
    Player player = new Player(playerInventory);

    Container missingInventory = new Container("No inventory", inventory: null);
    Container destroyed = Chest("Destroyed", Item("Iron", 5));
    destroyed.Destroyed = true;
    Container healthy = Chest("Healthy", Item("Iron", 5));
    List<Container> discovered = new List<Container> { missingInventory, destroyed, healthy };

    QuickStackEligibility eligibility = QuickStackTransfer.FindEligibleContainers(player, discovered);
    ExpectEqual(1, eligibility.Containers.Count, "selection skips unavailable targets and keeps the healthy chest");
    ExpectSame(healthy, eligibility.Containers[0], "the healthy eligible chest remains selected");
    ExpectEqual(0, QuickStackTransfer.FindCandidates(player, missingInventory).Count, "candidate scan skips a missing inventory");
    ExpectEqual(0, QuickStackTransfer.FindCandidates(player, destroyed).Count, "candidate scan skips a destroyed container");

    List<DepositCandidate> candidates = new List<DepositCandidate> { new DepositCandidate(iron) };
    Container unrelated = Chest("Unrelated", Item("Wood", 10));
    ExpectFalse(
        QuickStackTransfer.HasLaterCandidateDependency(candidates, new List<Container> { missingInventory, destroyed, unrelated }, 0),
        "dependency scan skips unavailable targets");
}

static void AllUnavailableContainersFinishWithoutReservation()
{
    TestWorld world = BeginTest();
    world.PlayerInventory.Add(Item("Iron"));
    Container missing = new Container("Not ready", inventory: null);
    Container destroyed = Chest("Destroyed", Item("Iron", 5));
    destroyed.Destroyed = true;
    NearbyContainerIndex.Containers = new List<Container> { missing, destroyed };

    QuickStack.Run(world.Player, world.Gui, currentContainer: null);
    string operationId = RequestedOperationId(world.Rpc);
    world.Rpc.Respond(operationId, PutAwayLeaseProtocol.Granted, "granted");
    QuickStack.Update();

    ExpectEqual(0, InventoryTransactions.BeginDepositCount, "unavailable-only discovery reserves no items");
    ExpectEqual(1, InventoryTransactions.Terminals.Count, "unavailable-only discovery completes the batch");
    ExpectEqual(operationId, InventoryTransactions.Terminals[0].OperationId, "unavailable-only terminal is correlated");
    ExpectEqual("completed", InventoryTransactions.Terminals[0].Status, "unavailable-only discovery completes safely");
    ExpectEqual("no_eligible_containers", InventoryTransactions.Terminals[0].Reason, "unavailable-only discovery has no eligible target");
    ExpectEqual(1, ReleaseCalls(world.Rpc).Count, "unavailable-only discovery releases its lease once");
}

static void NetworkShutdownAndDestroyReleaseAnOrphanedLeaseOnce()
{
    TestWorld world = BeginTest();
    string operationId = AcquireLeaseWithoutStartingBatch(world.Rpc);
    SetRuntimeInitialized(false);

    InvokeBeforeNetworkShutdown();
    InvokeBeforeNetworkDestroy();

    ExpectEqual(1, ReleaseCalls(world.Rpc).Count, "world teardown releases an orphaned lease even when the runtime was not initialized");
    ExpectEqual(operationId, ReleaseCalls(world.Rpc)[0].OperationId, "teardown release keeps the orphaned lease ID");
    ExpectEqual(0, InventoryTransactions.ShutdownCount, "uninitialized teardown does not shut down an uninitialized protocol");
    ExpectFalse(PutAwayLeaseClient.IsPendingOrHeld, "teardown clears the stranded local lease");

    ZRpc rejoinedRpc = new ZRpc();
    ZNet.instance = new ZNet(rejoinedRpc);
    QuickStack.Run(world.Player, world.Gui, currentContainer: null);
    string retryOperationId = RequestedOperationId(rejoinedRpc);
    ExpectNotEqual(operationId, retryOperationId, "rejoin can immediately request a new lease");
    rejoinedRpc.Respond(retryOperationId, PutAwayLeaseProtocol.Rejected, "busy");
    QuickStack.Update();
    ExpectFalse(PutAwayLeaseClient.IsPendingOrHeld, "rejoined retry is no longer blocked by the old lease");
}

static void DirectDestroyReleasesAnOrphanedLease()
{
    TestWorld world = BeginTest();
    string operationId = AcquireLeaseWithoutStartingBatch(world.Rpc);
    SetRuntimeInitialized(false);

    InvokeBeforeNetworkDestroy();

    ExpectEqual(1, ReleaseCalls(world.Rpc).Count, "direct ZNet destruction uses the orphan-lease fallback");
    ExpectEqual(operationId, ReleaseCalls(world.Rpc)[0].OperationId, "direct-destroy release keeps the operation ID");
    ExpectFalse(PutAwayLeaseClient.IsPendingOrHeld, "direct destruction clears the local lease");
}

static void NetworkShutdownAndDestroyPreserveAnUnsettledBatch()
{
    TestWorld world = BeginTest();
    string operationId = AcquireLeaseWithoutStartingBatch(world.Rpc);
    QuickStackOperation operation = new QuickStackOperation(
        operationId,
        PutAwayStageTiming.Start(),
        world.Player,
        world.Gui,
        new List<Container>(),
        inventoryWasOpen: false,
        scanMatchDurationMs: 0,
        terminalReady: _ => throw new InvalidOperationException("The fixture operation should not terminate."));
    ExpectTrue(operation.Pipeline.TryRequestValidation(() => true), "fixture pipeline accepts a validation");
    ExpectTrue(
        operation.Pipeline.TryBeginValidatedDeposit(
            _ => true,
            _ => { },
            () => { },
            () => { },
            _ => { },
            () => { }),
        "fixture pipeline records an unsettled deposit");
    SetActiveOperation(operation);
    InventoryTransactions.SetUnsettledForTest(true);
    SetRuntimeInitialized(true);

    InvokeBeforeNetworkShutdown();
    InvokeBeforeNetworkDestroy();

    ExpectEqual(true, InventoryTransactions.UnsettledAtShutdown, "the reset guard observes protocol state before shutdown clears it");
    ExpectEqual(1, InventoryTransactions.ShutdownCount, "initialized network teardown shuts down the protocol");
    ExpectEqual(0, ReleaseCalls(world.Rpc).Count, "teardown preserves the lease while a deposit remains unsettled");
    ExpectSame(operation, GetActiveOperation(), "teardown guard does not mark an unsettled batch safely cancelled");

    // Clear fixture-only state without exercising a second lifecycle path.
    SetActiveOperation(null);
    PutAwayLeaseClient.Reset();
}
