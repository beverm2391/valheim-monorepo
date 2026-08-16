using System;
using System.Collections.Generic;

namespace BenheimInventoryProtocol;

internal enum ServerRequestAction
{
    Route,
    Replay,
    Conflict,
    OwnerUnavailable
}

internal enum OwnerResultAction
{
    Complete,
    AwaitRetry,
    Reject
}

internal sealed class ServerRequestDecision
{
    private ServerRequestDecision(ServerRequestAction action, long owner, byte[]? responseBytes)
    {
        Action = action;
        Owner = owner;
        ResponseBytes = responseBytes;
    }

    internal ServerRequestAction Action { get; }
    internal long Owner { get; }
    internal byte[]? ResponseBytes { get; }

    internal static ServerRequestDecision Route(long owner) =>
        new ServerRequestDecision(ServerRequestAction.Route, owner, null);

    internal static ServerRequestDecision Replay(byte[] responseBytes) =>
        new ServerRequestDecision(ServerRequestAction.Replay, 0L, responseBytes);

    internal static ServerRequestDecision Conflict() =>
        new ServerRequestDecision(ServerRequestAction.Conflict, 0L, null);

    internal static ServerRequestDecision OwnerUnavailable() =>
        new ServerRequestDecision(ServerRequestAction.OwnerUnavailable, 0L, null);
}

/// <summary>
/// Connected-session server state for immutable Put Away requests. This is the
/// protocol's routing and deduplication authority; game-facing RPC code only
/// resolves the live ZDO owner and carries these decisions over the network.
/// </summary>
internal sealed class ConnectedTransactionRouter<TContainer>
    where TContainer : notnull
{
    private sealed class PendingRoute
    {
        internal PendingRoute(
            long requester,
            string payloadHash,
            byte[] requestBytes,
            TContainer container)
        {
            Requester = requester;
            PayloadHash = payloadHash;
            RequestBytes = requestBytes;
            Container = container;
        }

        internal long Requester { get; }
        internal string PayloadHash { get; }
        internal byte[] RequestBytes { get; }
        internal TContainer Container { get; }
        internal long RoutedOwner { get; set; }
    }

    private sealed class CompletedRoute
    {
        internal CompletedRoute(long requester, string payloadHash, byte[] responseBytes, float completedAt)
        {
            Requester = requester;
            PayloadHash = payloadHash;
            ResponseBytes = responseBytes;
            CompletedAt = completedAt;
        }

        internal long Requester { get; }
        internal string PayloadHash { get; }
        internal byte[] ResponseBytes { get; }
        internal float CompletedAt { get; }
    }

    private readonly Dictionary<string, PendingRoute> pending = new Dictionary<string, PendingRoute>();
    private readonly Dictionary<string, CompletedRoute> completed = new Dictionary<string, CompletedRoute>();

    internal ServerRequestDecision ReceiveRequest(
        string transactionId,
        long requester,
        string payloadHash,
        byte[] requestBytes,
        TContainer container,
        long currentOwner)
    {
        if (completed.TryGetValue(transactionId, out CompletedRoute? completedRoute))
        {
            return completedRoute.Requester == requester && completedRoute.PayloadHash == payloadHash
                ? ServerRequestDecision.Replay(completedRoute.ResponseBytes)
                : ServerRequestDecision.Conflict();
        }

        if (pending.TryGetValue(transactionId, out PendingRoute? pendingRoute))
        {
            if (pendingRoute.Requester != requester
                || pendingRoute.PayloadHash != payloadHash
                || !EqualityComparer<TContainer>.Default.Equals(pendingRoute.Container, container))
            {
                return ServerRequestDecision.Conflict();
            }
        }
        else
        {
            pendingRoute = new PendingRoute(requester, payloadHash, requestBytes, container);
            pending.Add(transactionId, pendingRoute);
        }

        if (currentOwner == 0L)
        {
            return ServerRequestDecision.OwnerUnavailable();
        }

        // Only the most recently resolved owner may settle this request. A set
        // of historical owners would let a delayed result from an old owner
        // win after the request had already been rerouted.
        pendingRoute.RoutedOwner = currentOwner;
        return ServerRequestDecision.Route(currentOwner);
    }

    internal bool TryGetPendingContainer(string transactionId, out TContainer container)
    {
        if (pending.TryGetValue(transactionId, out PendingRoute? route))
        {
            container = route.Container;
            return true;
        }

        container = default!;
        return false;
    }

    internal OwnerResultAction ReceiveOwnerResult(
        string transactionId,
        long requester,
        string payloadHash,
        long sender,
        long currentOwner,
        byte[] responseBytes,
        float completedAt,
        bool ownerReportedStale)
    {
        if (!pending.TryGetValue(transactionId, out PendingRoute? route)
            || route.Requester != requester
            || route.PayloadHash != payloadHash
            || sender == 0L
            || sender != route.RoutedOwner
            || sender != currentOwner)
        {
            return OwnerResultAction.Reject;
        }

        if (ownerReportedStale)
        {
            return OwnerResultAction.AwaitRetry;
        }

        pending.Remove(transactionId);
        completed[transactionId] = new CompletedRoute(
            requester,
            payloadHash,
            responseBytes,
            completedAt);
        return OwnerResultAction.Complete;
    }

    internal void ExpireCompleted(float olderThan)
    {
        List<string> expired = new List<string>();
        foreach (KeyValuePair<string, CompletedRoute> pair in completed)
        {
            if (pair.Value.CompletedAt < olderThan)
            {
                expired.Add(pair.Key);
            }
        }

        foreach (string transactionId in expired)
        {
            completed.Remove(transactionId);
        }
    }

    internal void Clear()
    {
        pending.Clear();
        completed.Clear();
    }
}
