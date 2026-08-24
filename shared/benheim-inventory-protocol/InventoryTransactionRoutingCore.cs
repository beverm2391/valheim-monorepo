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
    private ServerRequestDecision(
        ServerRequestAction action,
        long owner,
        byte[]? responseBytes,
        bool rerouted)
    {
        Action = action;
        Owner = owner;
        ResponseBytes = responseBytes;
        Rerouted = rerouted;
    }

    internal ServerRequestAction Action { get; }
    internal long Owner { get; }
    internal byte[]? ResponseBytes { get; }
    internal bool Rerouted { get; }

    internal static ServerRequestDecision Route(long owner, bool rerouted) =>
        new ServerRequestDecision(ServerRequestAction.Route, owner, null, rerouted);

    internal static ServerRequestDecision Replay(byte[] responseBytes) =>
        new ServerRequestDecision(ServerRequestAction.Replay, 0L, responseBytes, false);

    internal static ServerRequestDecision Conflict() =>
        new ServerRequestDecision(ServerRequestAction.Conflict, 0L, null, false);

    internal static ServerRequestDecision OwnerUnavailable() =>
        new ServerRequestDecision(ServerRequestAction.OwnerUnavailable, 0L, null, false);
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
        internal CompletedRoute(
            long requester,
            string payloadHash,
            TContainer container,
            byte[] responseBytes,
            float completedAt)
        {
            Requester = requester;
            PayloadHash = payloadHash;
            Container = container;
            ResponseBytes = responseBytes;
            CompletedAt = completedAt;
        }

        internal long Requester { get; }
        internal string PayloadHash { get; }
        internal TContainer Container { get; }
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
        bool rerouted = pendingRoute.RoutedOwner != 0L
            && pendingRoute.RoutedOwner != currentOwner;
        pendingRoute.RoutedOwner = currentOwner;
        return ServerRequestDecision.Route(currentOwner, rerouted);
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
            route.Container,
            responseBytes,
            completedAt);
        return OwnerResultAction.Complete;
    }

    internal bool MatchesCompleted(
        string transactionId,
        long requester,
        string payloadHash,
        TContainer container)
    {
        return completed.TryGetValue(transactionId, out CompletedRoute? route)
            && route.Requester == requester
            && route.PayloadHash == payloadHash
            && EqualityComparer<TContainer>.Default.Equals(route.Container, container);
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

    internal int RemoveRequester(long requester)
    {
        List<string> pendingTransactions = new List<string>();
        foreach (KeyValuePair<string, PendingRoute> pair in pending)
        {
            if (pair.Value.Requester == requester)
            {
                pendingTransactions.Add(pair.Key);
            }
        }

        List<string> completedTransactions = new List<string>();
        foreach (KeyValuePair<string, CompletedRoute> pair in completed)
        {
            if (pair.Value.Requester == requester)
            {
                completedTransactions.Add(pair.Key);
            }
        }

        foreach (string transactionId in pendingTransactions)
        {
            pending.Remove(transactionId);
        }

        foreach (string transactionId in completedTransactions)
        {
            completed.Remove(transactionId);
        }

        return pendingTransactions.Count + completedTransactions.Count;
    }

    internal void Clear()
    {
        pending.Clear();
        completed.Clear();
    }
}
