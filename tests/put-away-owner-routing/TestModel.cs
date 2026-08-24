using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BenheimInventoryProtocol;

internal sealed class Chest
{
    private readonly IReadOnlyDictionary<string, int>? capacity;
    private readonly Dictionary<string, DepositResult> receipts = new(StringComparer.Ordinal);

    internal Chest(IReadOnlyDictionary<string, int> initial, IReadOnlyDictionary<string, int>? capacity = null)
    {
        Items = new Dictionary<string, int>(initial, StringComparer.Ordinal);
        this.capacity = capacity;
    }

    internal Dictionary<string, int> Items { get; }
    internal uint Revision { get; private set; }

    internal DepositResult ApplyOwnerAuthoritative(DepositRequest request)
    {
        if (receipts.TryGetValue(request.OperationId, out DepositResult? receipt))
        {
            if (receipt.PayloadHash != request.PayloadHash)
            {
                throw new InvalidOperationException("transaction identity reused with a different payload");
            }

            return receipt;
        }

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
        DepositResult result = new DepositResult(request.OperationId, request.PayloadHash, accepted);
        receipts[request.OperationId] = result;
        return result;
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
