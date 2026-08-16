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

// A delayed result from an owner that was valid for an earlier route must not
// settle the request after the server has rerouted it to a new current owner.
ConnectedTransactionRouter<string> handoffRouter = new();
DepositRequest handoffRequest = new(
    "operation-handoff",
    "hash-handoff",
    Counts(("Stone", 3)));
byte[] handoffRequestBytes = Encoding.UTF8.GetBytes("immutable-handoff-request");
ServerRequestDecision routedToOldOwner = handoffRouter.ReceiveRequest(
    handoffRequest.OperationId,
    requester: 10L,
    handoffRequest.PayloadHash,
    handoffRequestBytes,
    container: "chest-handoff",
    currentOwner: 20L);
Expect(routedToOldOwner.Action == ServerRequestAction.Route && routedToOldOwner.Owner == 20L,
    "request initially routes to the resolved owner");
ServerRequestDecision reroutedToCurrentOwner = handoffRouter.ReceiveRequest(
    handoffRequest.OperationId,
    requester: 10L,
    handoffRequest.PayloadHash,
    handoffRequestBytes,
    container: "chest-handoff",
    currentOwner: 30L);
Expect(reroutedToCurrentOwner.Action == ServerRequestAction.Route && reroutedToCurrentOwner.Owner == 30L,
    "connected retry reroutes after ownership changes");
OwnerResultAction delayedOldOwner = handoffRouter.ReceiveOwnerResult(
    handoffRequest.OperationId,
    requester: 10L,
    handoffRequest.PayloadHash,
    sender: 20L,
    currentOwner: 30L,
    responseBytes: Encoding.UTF8.GetBytes("old-owner-success"),
    completedAt: 1f,
    ownerReportedStale: false);
Expect(delayedOldOwner == OwnerResultAction.Reject,
    "delayed success from the old owner cannot settle a rerouted request");
OwnerResultAction currentOwnerResult = handoffRouter.ReceiveOwnerResult(
    handoffRequest.OperationId,
    requester: 10L,
    handoffRequest.PayloadHash,
    sender: 30L,
    currentOwner: 30L,
    responseBytes: Encoding.UTF8.GetBytes("current-owner-success"),
    completedAt: 2f,
    ownerReportedStale: false);
Expect(currentOwnerResult == OwnerResultAction.Complete,
    "only the latest routed current owner can settle the request");

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

internal sealed class Chest
{
    private readonly IReadOnlyDictionary<string, int>? capacity;

    internal Chest(IReadOnlyDictionary<string, int> initial, IReadOnlyDictionary<string, int>? capacity = null)
    {
        Items = new Dictionary<string, int>(initial, StringComparer.Ordinal);
        this.capacity = capacity;
    }

    internal Dictionary<string, int> Items { get; }
    internal uint Revision { get; private set; }

    internal DepositResult ApplyOwnerAuthoritative(DepositRequest request)
    {
        Dictionary<string, int> accepted = new(StringComparer.Ordinal);
        foreach ((string item, int count) in request.Reserved)
        {
            int before = CountsUtil.Get(Items, item);
            int room = capacity == null ? count : Math.Max(0, CountsUtil.Get(capacity, item) - before);
            int acceptedCount = Math.Min(count, room);
            Items[item] = before + acceptedCount;
            accepted[item] = acceptedCount;
        }

        Revision++;
        return new DepositResult(request.OperationId, request.PayloadHash, accepted);
    }

    internal void ApplyRequesterCache(Client client, string operationId, IReadOnlyDictionary<string, int> deposit)
    {
        client.Reserve(operationId);
        Items.Clear();
        foreach ((string item, int count) in client.Cache)
        {
            Items[item] = count;
        }
        foreach ((string item, int count) in deposit)
        {
            Items[item] = CountsUtil.Get(Items, item) + count;
        }
        Revision++;
    }

    internal void ReplicateTo(params Client[] clients)
    {
        foreach (Client client in clients)
        {
            client.Cache.ReplaceWith(Items);
            client.ObservedRevision = Revision;
        }
    }

    internal bool CountsEqual(IReadOnlyDictionary<string, int> expected) => Items.CountsEqual(expected);
}

internal sealed class Client
{
    private DepositRequest? pending;

    internal Client(string name, IReadOnlyDictionary<string, int> source, IReadOnlyDictionary<string, int> cache)
    {
        Name = name;
        Source = new Dictionary<string, int>(source, StringComparer.Ordinal);
        Cache = new Dictionary<string, int>(cache, StringComparer.Ordinal);
    }

    internal string Name { get; }
    internal Dictionary<string, int> Source { get; }
    internal Dictionary<string, int> Cache { get; }
    internal uint ObservedRevision { get; set; }
    internal string? PendingOperation => pending?.OperationId;

    internal DepositRequest Reserve(string operationId)
    {
        if (pending != null)
        {
            throw new InvalidOperationException("one in-flight request per client");
        }

        Dictionary<string, int> reserved = new(Source, StringComparer.Ordinal);
        foreach (string item in Source.Keys.ToList())
        {
            Source[item] = 0;
        }

        pending = new DepositRequest(operationId, Hash(operationId, reserved), reserved);
        return pending;
    }

    internal void Accept(DepositResult result)
    {
        if (pending == null || result.OperationId != pending.OperationId || result.PayloadHash != pending.PayloadHash)
        {
            throw new InvalidOperationException("uncorrelated result");
        }

        List<string> itemNames = pending.Reserved.Keys.ToList();
        List<int> reservedCounts = itemNames.Select(item => pending.Reserved[item]).ToList();
        List<int> reportedAccepted = itemNames.Select(item => CountsUtil.Get(result.Accepted, item)).ToList();
        if (!InventoryTransactionSettlement.TryCreate(
                reservedCounts,
                reportedAccepted,
                out InventoryTransactionSettlement? settlement))
        {
            throw new InvalidOperationException("invalid settlement");
        }

        for (int index = 0; index < itemNames.Count; index++)
        {
            string item = itemNames[index];
            Source[item] = CountsUtil.Get(Source, item) + settlement!.Rejected[index];
        }

        pending = null;
    }

    private static string Hash(string operationId, IReadOnlyDictionary<string, int> items) =>
        string.Join("|", new[] { operationId }.Concat(items.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}")));
}

internal sealed class Server
{
    private readonly Chest owner;
    private readonly ConnectedTransactionRouter<string> router = new();

    internal Server(Chest owner)
    {
        this.owner = owner;
    }

    internal DepositResult Route(DepositRequest request)
    {
        const long requester = 101L;
        const long currentOwner = 202L;
        byte[] requestBytes = Encoding.UTF8.GetBytes(request.PayloadHash);
        ServerRequestDecision decision = router.ReceiveRequest(
            request.OperationId,
            requester,
            request.PayloadHash,
            requestBytes,
            container: "authoritative-chest",
            currentOwner);
        if (decision.Action == ServerRequestAction.Replay)
        {
            return DecodeResult(decision.ResponseBytes!);
        }

        if (decision.Action != ServerRequestAction.Route || decision.Owner != currentOwner)
        {
            throw new InvalidOperationException($"unexpected route decision {decision.Action}");
        }

        DepositResult result = owner.ApplyOwnerAuthoritative(request);
        byte[] responseBytes = EncodeResult(result);
        OwnerResultAction ownerResult = router.ReceiveOwnerResult(
            request.OperationId,
            requester,
            request.PayloadHash,
            sender: currentOwner,
            currentOwner,
            responseBytes,
            completedAt: owner.Revision,
            ownerReportedStale: false);
        if (ownerResult != OwnerResultAction.Complete)
        {
            throw new InvalidOperationException($"unexpected owner result {ownerResult}");
        }

        return result;
    }

    private static byte[] EncodeResult(DepositResult result)
    {
        string accepted = string.Join(",", result.Accepted
            .OrderBy(pair => pair.Key)
            .Select(pair => $"{pair.Key}={pair.Value}"));
        return Encoding.UTF8.GetBytes($"{result.OperationId}\n{result.PayloadHash}\n{accepted}");
    }

    private static DepositResult DecodeResult(byte[] bytes)
    {
        string[] parts = Encoding.UTF8.GetString(bytes).Split('\n');
        Dictionary<string, int> accepted = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Split('='))
            .ToDictionary(values => values[0], values => int.Parse(values[1]), StringComparer.Ordinal);
        return new DepositResult(parts[0], parts[1], accepted);
    }
}

internal sealed record DepositRequest(
    string OperationId,
    string PayloadHash,
    IReadOnlyDictionary<string, int> Reserved);

internal sealed record DepositResult(
    string OperationId,
    string PayloadHash,
    IReadOnlyDictionary<string, int> Accepted);

internal static class CountExtensions
{
    internal static bool CountsEqual(this IReadOnlyDictionary<string, int> actual, IReadOnlyDictionary<string, int> expected) =>
        actual.Count == expected.Count && actual.All(pair => Get(expected, pair.Key) == pair.Value);

    internal static void ReplaceWith(this Dictionary<string, int> target, IReadOnlyDictionary<string, int> source)
    {
        target.Clear();
        foreach ((string item, int count) in source)
        {
            target[item] = count;
        }
    }

    private static int Get(IReadOnlyDictionary<string, int> values, string item) =>
        values.TryGetValue(item, out int count) ? count : 0;
}

internal static class CountsUtil
{
    internal static int Get(IReadOnlyDictionary<string, int> values, string item) =>
        values.TryGetValue(item, out int count) ? count : 0;
}
