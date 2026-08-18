using System.Collections.ObjectModel;
using System.Text;
using BenheimInventoryProtocol;

// Claim: a requester cache may be stale even after it observes the latest ZDO
// revision. Put Away remains safe only when the current chest owner applies the
// correlated request to its authoritative inventory and returns accepted counts.

var initialChest = Counts(("Stone", 2), ("Resin", 30));
var sourceA = Counts(("Stone", 15), ("Resin", 9));
var sourceB = Counts(("Stone", 20), ("Resin", 10));
var expected = Counts(("Stone", 37), ("Resin", 49));

// Sensitivity control for the exact 0.1.62 regression. B observes A's revision
// transition but its in-memory payload is still the original 2 Stone/30 Resin.
// A requester-local write therefore overwrites A's deposit.
Chest unsafeChest = new(initialChest);
Client unsafeA = new("A", sourceA, initialChest);
Client unsafeB = new("B", sourceB, initialChest);
unsafeChest.ApplyRequesterCache(unsafeA, "unsafe-a", sourceA);
unsafeB.ObservedRevision = unsafeChest.Revision;
unsafeChest.ApplyRequesterCache(unsafeB, "unsafe-b", sourceB);
Expect(unsafeB.ObservedRevision == 1, "control B observed A's revision transition");
Expect(unsafeChest.CountsEqual(Counts(("Stone", 22), ("Resin", 40))), "control reproduces stale overwrite");
Expect(!Conserved(initialChest, sourceA, sourceB, unsafeChest.Items, unsafeA.Source, unsafeB.Source),
    "control detects lost items instead of blessing the stale-write path");

// Required path. Neither requester's cached payload is used for the write.
Chest authoritativeChest = new(initialChest);
Client clientA = new("A", sourceA, initialChest);
Client clientB = new("B", sourceB, initialChest);
Server server = new(authoritativeChest);

DepositRequest requestA = clientA.Reserve("operation-a");
DepositResult resultA = server.Route(requestA);
DepositResult duplicateA = server.Route(requestA);
Expect(
    duplicateA.OperationId == resultA.OperationId
        && duplicateA.PayloadHash == resultA.PayloadHash
        && duplicateA.Accepted.CountsEqual(resultA.Accepted),
    "connected retry returns the correlated cached result");
Expect(authoritativeChest.Revision == 1, "connected retry does not invoke the owner twice");
Expect(authoritativeChest.CountsEqual(Counts(("Stone", 17), ("Resin", 39))),
    "connected retry does not apply A twice");
clientA.Accept(resultA);
clientB.ObservedRevision = authoritativeChest.Revision;
Expect(clientB.Cache.CountsEqual(initialChest), "B remains deliberately stale after observing A's revision");

DepositResult resultB = server.Route(clientB.Reserve("operation-b"));
clientB.Accept(resultB);
authoritativeChest.ReplicateTo(clientA, clientB);

Expect(authoritativeChest.CountsEqual(expected), "authoritative chest contains base + A + B");
Expect(clientA.Cache.CountsEqual(expected), "client A converges to authoritative contents");
Expect(clientB.Cache.CountsEqual(expected), "client B converges to authoritative contents");
Expect(Conserved(initialChest, sourceA, sourceB, authoritativeChest.Items, clientA.Source, clientB.Source),
    "exact player/chest conservation holds");
Expect(clientA.Source.Values.Sum() == 0 && clientB.Source.Values.Sum() == 0,
    "each accepted amount is removed from its requester exactly once");

// Partial capacity returns the rejected remainder without changing the
// accepted amount or total item count.
Chest partialChest = new(Counts(("Stone", 48)), Counts(("Stone", 50)));
Client partialClient = new("partial", Counts(("Stone", 10)), Counts(("Stone", 48)));
DepositResult partialResult = new Server(partialChest).Route(partialClient.Reserve("operation-partial"));
partialClient.Accept(partialResult);
Expect(partialChest.CountsEqual(Counts(("Stone", 50))), "owner clamps to live chest capacity");
Expect(partialClient.Source["Stone"] == 8, "requester restores the exact rejected remainder");
Expect(Get(Counts(("Stone", 48)), "Stone") + Get(Counts(("Stone", 10)), "Stone")
    == Get(partialChest.Items, "Stone") + Get(partialClient.Source, "Stone"),
    "partial capacity conserves exact counts");

// Correlation is part of the conservation boundary. A result for another
// operation cannot settle or refund this request.
Client correlationClient = new("correlation", Counts(("Stone", 4)), initialChest);
DepositRequest correlationRequest = correlationClient.Reserve("operation-c");
ExpectThrows(() => correlationClient.Accept(new DepositResult(
    "other-operation",
    correlationRequest.PayloadHash,
    Counts(("Stone", 4)))), "mismatched operation is rejected");
Expect(correlationClient.PendingOperation == "operation-c" && correlationClient.Source["Stone"] == 0,
    "mismatched result leaves the reservation pending rather than duplicating it");

Expect(
    !InventoryTransactionSettlement.TryCreate(
        new[] { 4, 6 },
        new[] { 4 },
        out _),
    "partial non-success result without an exact accepted vector remains pending");

Expect(
    InventoryTransactionRefundPolicy.Decide(
        restoredToOriginalSlot: false,
        restoredElsewhere: false) == InventoryTransactionRefundPlacement.WorldDrop,
    "filled original slot and filled inventory require a visible nearby refund drop");
Expect(
    InventoryTransactionRefundPolicy.Decide(
        restoredToOriginalSlot: false,
        restoredElsewhere: true) == InventoryTransactionRefundPlacement.Inventory,
    "rejected remainder stays in inventory when another slot has room");

Expect(
    InventoryTransactionSettlement.TryCreate(
        new[] { 10 },
        new[] { 2 },
        out InventoryTransactionSettlement? filledInventorySettlement),
    "filled-slot partial acceptance produces an exact settlement");
int filledInventoryDrop = filledInventorySettlement!.Rejected.Single();
Expect(
    InventoryTransactionRefundPolicy.Decide(
        restoredToOriginalSlot: false,
        restoredElsewhere: false) == InventoryTransactionRefundPlacement.WorldDrop
        && filledInventoryDrop == 8,
    "filled-slot partial rejection drops the exact eight-item remainder nearby");
Expect(2 + filledInventoryDrop == 10,
    "filled-slot partial acceptance conserves accepted and dropped counts");
Expect(!InventoryTransactionLifecyclePolicy.CanSettle(localPlayerAvailable: false),
    "owner result remains pending when no local player can receive a refund drop");
Expect(InventoryTransactionLifecyclePolicy.CanSettle(localPlayerAvailable: true),
    "owner result can settle when the local player can receive every remainder");
Expect(!InventoryTransactionLifecyclePolicy.CanResetBatch(hasUnsettledDeposit: true),
    "context reset cannot release a batch before exact settlement");
Expect(InventoryTransactionLifecyclePolicy.CanResetBatch(hasUnsettledDeposit: false),
    "context reset remains available when no deposit is settling");

// Ownership changes after the old owner applies and records its receipt but
// before the server accepts that result. The new owner must replay that
// receipt instead of applying the immutable deposit twice.
ConnectedTransactionRouter<string> handoffRouter = new();
Chest handoffChest = new(Counts(("Stone", 2)));
Client handoffClient = new("handoff", Counts(("Stone", 3)), Counts(("Stone", 2)));
DepositRequest handoffRequest = handoffClient.Reserve("operation-handoff");
byte[] handoffRequestBytes = Encoding.UTF8.GetBytes(handoffRequest.PayloadHash);
ServerRequestDecision routedToOldOwner = handoffRouter.ReceiveRequest(
    handoffRequest.OperationId,
    requester: 10L,
    handoffRequest.PayloadHash,
    handoffRequestBytes,
    container: "chest-handoff",
    currentOwner: 20L);
Expect(routedToOldOwner.Action == ServerRequestAction.Route && routedToOldOwner.Owner == 20L,
    "request initially routes to the resolved owner");
DepositResult oldOwnerApplied = handoffChest.ApplyOwnerAuthoritative(handoffRequest);
byte[] oldOwnerResponse = Encoding.UTF8.GetBytes("old-owner-applied");
OwnerResultAction oldOwnerLostRace = handoffRouter.ReceiveOwnerResult(
    handoffRequest.OperationId,
    requester: 10L,
    handoffRequest.PayloadHash,
    sender: 20L,
    currentOwner: 30L,
    responseBytes: oldOwnerResponse,
    completedAt: 1f,
    ownerReportedStale: false);
Expect(oldOwnerLostRace == OwnerResultAction.Reject,
    "owner result cannot settle after ownership changes");
ServerRequestDecision reroutedToCurrentOwner = handoffRouter.ReceiveRequest(
    handoffRequest.OperationId,
    requester: 10L,
    handoffRequest.PayloadHash,
    handoffRequestBytes,
    container: "chest-handoff",
    currentOwner: 30L);
Expect(reroutedToCurrentOwner.Action == ServerRequestAction.Route && reroutedToCurrentOwner.Owner == 30L,
    "connected retry reroutes after ownership changes");
DepositResult newOwnerReplay = handoffChest.ApplyOwnerAuthoritative(handoffRequest);
Expect(handoffChest.Revision == 1 && newOwnerReplay.Accepted.CountsEqual(oldOwnerApplied.Accepted),
    "new owner replays the receipt without applying twice");
OwnerResultAction delayedOldOwner = handoffRouter.ReceiveOwnerResult(
    handoffRequest.OperationId,
    requester: 10L,
    handoffRequest.PayloadHash,
    sender: 20L,
    currentOwner: 30L,
    responseBytes: oldOwnerResponse,
    completedAt: 2f,
    ownerReportedStale: false);
Expect(delayedOldOwner == OwnerResultAction.Reject,
    "delayed success from the old owner cannot settle a rerouted request");
OwnerResultAction currentOwnerResult = handoffRouter.ReceiveOwnerResult(
    handoffRequest.OperationId,
    requester: 10L,
    handoffRequest.PayloadHash,
    sender: 30L,
    currentOwner: 30L,
    responseBytes: Encoding.UTF8.GetBytes("new-owner-receipt-replay"),
    completedAt: 3f,
    ownerReportedStale: false);
Expect(currentOwnerResult == OwnerResultAction.Complete,
    "only the latest routed current owner can settle the request");
Expect(
    handoffRouter.MatchesCompleted(
        handoffRequest.OperationId,
        requester: 10L,
        handoffRequest.PayloadHash,
        container: "chest-handoff"),
    "receipt acknowledgement matches the completed requester, payload, and chest");
Expect(
    !handoffRouter.MatchesCompleted(
        handoffRequest.OperationId,
        requester: 10L,
        handoffRequest.PayloadHash,
        container: "other-chest"),
    "receipt acknowledgement cannot clear another chest receipt");
handoffRouter.ExpireCompleted(olderThan: 2f);
Expect(
    handoffRouter.MatchesCompleted(
        handoffRequest.OperationId,
        requester: 10L,
        handoffRequest.PayloadHash,
        container: "chest-handoff"),
    "completed correlation remains for the connected retry window");
handoffRouter.ExpireCompleted(olderThan: 4f);
Expect(
    !handoffRouter.MatchesCompleted(
        handoffRequest.OperationId,
        requester: 10L,
        handoffRequest.PayloadHash,
        container: "chest-handoff"),
    "completed correlation expires after its replay window");
handoffClient.Accept(newOwnerReplay);
handoffChest.ReplicateTo(handoffClient);
Expect(handoffChest.CountsEqual(Counts(("Stone", 5))) && handoffClient.Source["Stone"] == 0,
    "post-apply ownership handoff conserves the deposit exactly once");
Expect(handoffClient.Cache.CountsEqual(handoffChest.Items),
    "post-apply ownership handoff converges requester and owner contents");

Console.WriteLine("Put Away owner-authoritative stale-payload integration checks passed.");

static Dictionary<string, int> Counts(params (string Item, int Count)[] entries) =>
    entries.ToDictionary(entry => entry.Item, entry => entry.Count, StringComparer.Ordinal);

static bool Conserved(
    IReadOnlyDictionary<string, int> initialChest,
    IReadOnlyDictionary<string, int> initialA,
    IReadOnlyDictionary<string, int> initialB,
    IReadOnlyDictionary<string, int> finalChest,
    IReadOnlyDictionary<string, int> finalA,
    IReadOnlyDictionary<string, int> finalB)
{
    foreach (string item in initialChest.Keys.Concat(initialA.Keys).Concat(initialB.Keys).Distinct())
    {
        int before = Get(initialChest, item) + Get(initialA, item) + Get(initialB, item);
        int after = Get(finalChest, item) + Get(finalA, item) + Get(finalB, item);
        if (before != after)
        {
            return false;
        }
    }

    return true;
}

static int Get(IReadOnlyDictionary<string, int> values, string item) => CountsUtil.Get(values, item);

static void Expect(bool condition, string claim)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Failed: {claim}");
    }
}

static void ExpectThrows(Action action, string claim)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException($"Failed: {claim}");
}
