using BenheimQoL.Infrastructure;
using BenheimQoL.KillAttribution;

namespace BenheimServerSupport;

/// <summary>
/// Owns ordered, bounded delivery of authoritative chain transitions. This is
/// deliberately separate from chain qualification and counting: transport
/// failure may retry a fact, but it cannot invent or reorder chain state.
/// </summary>
internal static class KillChainDeliveryRuntime
{
    private const int MaximumPendingTransitionsPerKiller = 32;
    private const int MaximumDeliveryAttempts = 5;
    private const double DeliveryRetrySeconds = 0.5d;

    private static readonly KillChainDeliveryQueue<ZDOID, PendingChainDelivery> Pending =
        new KillChainDeliveryQueue<ZDOID, PendingChainDelivery>(
            MaximumPendingTransitionsPerKiller);

    internal static void Update(
        double serverTimeSeconds,
        KillChainState<ZDOID> chains)
    {
        for (int index = Pending.KillerCount - 1; index >= 0; index--)
        {
            ZDOID killer = Pending.KillerAt(index);
            if (!Pending.TryPeek(killer, out PendingChainDelivery pending)
                || serverTimeSeconds < pending.RetryAt)
            {
                continue;
            }

            ZNetPeer? killerPeer = KillAttributionServer.FindReadyPeer(killer);
            if (killerPeer == null)
            {
                Abandon(pending.Transition, pending.Reason, "killer_not_connected", chains);
                continue;
            }

            if (TrySend(killerPeer, pending.Transition, out string failure))
            {
                Pending.MarkDelivered(killer);
                Emit(
                    pending.Transition,
                    pending.Reason,
                    "delivered_after_retry",
                    string.Empty);
                continue;
            }

            int attempts = pending.AttemptCount + 1;
            if (attempts >= MaximumDeliveryAttempts)
            {
                Abandon(pending.Transition, pending.Reason, failure, chains);
                continue;
            }

            Pending.ReplaceHead(
                killer,
                pending.WithRetry(attempts, serverTimeSeconds + DeliveryRetrySeconds));
        }
    }

    internal static void Deliver(
        KillChainTransition<ZDOID> transition,
        string reason,
        KillChainState<ZDOID> chains)
    {
        ZNetPeer? killerPeer = KillAttributionServer.FindReadyPeer(transition.Killer);
        if (killerPeer == null)
        {
            Emit(transition, reason, "not_delivered", "killer_not_connected");
            return;
        }

        Deliver(killerPeer, transition, reason, chains);
    }

    internal static void Deliver(
        ZNetPeer killerPeer,
        KillChainTransition<ZDOID> transition,
        string reason,
        KillChainState<ZDOID> chains)
    {
        if (Pending.HasPending(transition.Killer))
        {
            Queue(
                new PendingChainDelivery(transition, reason, attemptCount: 0, retryAt: 0d),
                "ordered_after_pending",
                chains);
            return;
        }

        if (TrySend(killerPeer, transition, out string failure))
        {
            Emit(transition, reason, "delivered", string.Empty);
            return;
        }

        Queue(
            new PendingChainDelivery(
                transition,
                reason,
                attemptCount: 1,
                retryAt: transition.ServerTimeSeconds + DeliveryRetrySeconds),
            failure,
            chains);
    }

    internal static void RemoveKiller(ZDOID killer)
    {
        Pending.RemoveKiller(killer);
    }

    internal static void Reset()
    {
        Pending.Reset();
    }

    private static bool TrySend(
        ZNetPeer killerPeer,
        KillChainTransition<ZDOID> transition,
        out string failure)
    {
        return KillChainDeliveryAttempt.TrySend(
            killerPeer.m_rpc.IsConnected(),
            () => killerPeer.m_rpc.Invoke(
                KillAttributionProtocol.ChainTransitionRpc,
                KillAttributionProtocol.BuildChainTransition(
                    new KillChainTransitionMessage(
                        transition.Killer,
                        transition.Kind,
                        transition.Tier,
                        transition.KillCount,
                        transition.ServerSequence,
                        transition.ServerTimeSeconds,
                        transition.ExpiresAtServerTimeSeconds))),
            out failure);
    }

    private static void Queue(
        PendingChainDelivery pending,
        string failure,
        KillChainState<ZDOID> chains)
    {
        if (Pending.Enqueue(pending.Transition.Killer, pending))
        {
            Emit(pending.Transition, pending.Reason, "queued", failure);
            return;
        }

        Abandon(pending.Transition, pending.Reason, "queue_full", chains);
    }

    private static void Abandon(
        KillChainTransition<ZDOID> transition,
        string reason,
        string failure,
        KillChainState<ZDOID> chains)
    {
        Pending.RemoveKiller(transition.Killer);
        chains.RemoveKiller(transition.Killer);
        Emit(transition, reason, "abandoned", failure);
    }

    private static void Emit(
        KillChainTransition<ZDOID> transition,
        string reason,
        string status,
        string failure)
    {
        ServerDiagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "kill_chain_transition")
                .String("operation_phase", "chain_transition")
                .String("status", status)
                .String("reason", reason)
                .String("failure", failure)
                .String("killer_id", transition.Killer.ToString())
                .String("transition", transition.Kind.ToString())
                .String("tier", transition.Tier.ToString())
                .Integer("kill_count", transition.KillCount)
                .Integer("server_sequence", transition.ServerSequence)
                .Number("server_time_seconds", transition.ServerTimeSeconds)
                .Number(
                    "expires_at_server_time_seconds",
                    transition.ExpiresAtServerTimeSeconds));
    }

    private readonly struct PendingChainDelivery
    {
        internal PendingChainDelivery(
            KillChainTransition<ZDOID> transition,
            string reason,
            int attemptCount,
            double retryAt)
        {
            Transition = transition;
            Reason = reason;
            AttemptCount = attemptCount;
            RetryAt = retryAt;
        }

        internal KillChainTransition<ZDOID> Transition { get; }
        internal string Reason { get; }
        internal int AttemptCount { get; }
        internal double RetryAt { get; }

        internal PendingChainDelivery WithRetry(int attemptCount, double retryAt)
        {
            return new PendingChainDelivery(Transition, Reason, attemptCount, retryAt);
        }
    }
}
