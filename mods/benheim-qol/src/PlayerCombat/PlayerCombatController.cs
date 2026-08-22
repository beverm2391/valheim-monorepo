using System;
using System.Collections.Generic;

namespace BenheimQoL.PlayerCombat;

internal enum EarnedStateOutputOutcome
{
    Activated,
    Refreshed,
    Rejected
}

internal readonly struct EarnedStateOutputResult
{
    private EarnedStateOutputResult(
        EarnedStateOutputOutcome outcome,
        EarnedStateTransitionReason reason)
    {
        Outcome = outcome;
        Reason = reason;
    }

    internal EarnedStateOutputOutcome Outcome { get; }
    internal EarnedStateTransitionReason Reason { get; }

    internal static EarnedStateOutputResult Activated() =>
        new EarnedStateOutputResult(
            EarnedStateOutputOutcome.Activated,
            EarnedStateTransitionReason.NativeEffectApplied);

    internal static EarnedStateOutputResult Refreshed() =>
        new EarnedStateOutputResult(
            EarnedStateOutputOutcome.Refreshed,
            EarnedStateTransitionReason.NativeEffectRefreshed);

    internal static EarnedStateOutputResult Rejected(EarnedStateTransitionReason reason) =>
        new EarnedStateOutputResult(EarnedStateOutputOutcome.Rejected, reason);
}

internal interface IEarnedStateOutput
{
    EarnedStateOutputResult Activate(
        Player player,
        EarnedCombatState state,
        int tier,
        float? durationSeconds = null);
    void Deactivate(Player player, EarnedCombatState state, int tier);
}

internal interface IPlayerCombatFactPublisher
{
    void Publish(ClutchDecision decision);
    void Publish(UntouchableProgress progress);
    void Publish(UntouchableReset reset);
    void Publish(EarnedStateTransition transition);
}

/// <summary>
/// Owns one player's ephemeral combat progress and earned states. Feature rules
/// make decisions here, while native effects and presentation remain behind
/// shared adapters.
/// </summary>
internal sealed class PlayerCombatController
{
    private readonly Player player;
    private readonly IEarnedStateOutput output;
    private readonly IPlayerCombatFactPublisher facts;
    private readonly Dictionary<EarnedCombatState, int> activeStates =
        new Dictionary<EarnedCombatState, int>();

    internal PlayerCombatController(
        Player player,
        IEarnedStateOutput output,
        IPlayerCombatFactPublisher facts)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.output = output ?? throw new ArgumentNullException(nameof(output));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
    }

    internal int UntouchableStreak { get; private set; }

    internal void Observe(PerfectDefenseConfirmed perfectDefense)
    {
        if (perfectDefense.Context.Player != player)
        {
            return;
        }

        ClutchDecision decision = ClutchMechanic.Decide(
            perfectDefense,
            HasEarned(EarnedCombatState.Clutch));
        facts.Publish(decision);
        if (decision.Outcome != ClutchDecisionOutcome.Reject)
        {
            Earn(perfectDefense.Context, EarnedCombatState.Clutch, ClutchMechanic.Tier);
        }

        AdvanceUntouchable(
            perfectDefense.Context,
            UntouchableProgressSource.PerfectDefense,
            perfectDefense.Kind,
            serverSequence: null);
    }

    internal void Observe(ConfirmedKill confirmedKill)
    {
        if (confirmedKill.Killer.Player != player)
        {
            return;
        }

        AdvanceUntouchable(
            confirmedKill.Killer,
            UntouchableProgressSource.ConfirmedKill,
            defense: null,
            confirmedKill.ServerSequence);
    }

    private void AdvanceUntouchable(
        PlayerCombatContext context,
        UntouchableProgressSource source,
        PerfectDefenseKind? defense,
        long? serverSequence)
    {
        int previousStreak = UntouchableStreak;
        int previousUntouchableTier = EarnedTier(EarnedCombatState.Untouchable);
        UntouchableStreak++;

        int earnedUntouchableTier = UntouchableMechanic.TierForStreak(
            UntouchableStreak);
        UntouchableProgressOutcome progressOutcome = UntouchableProgressOutcome.StreakIncremented;
        if (earnedUntouchableTier > previousUntouchableTier)
        {
            Earn(
                context,
                EarnedCombatState.Untouchable,
                earnedUntouchableTier);
        }

        int currentUntouchableTier = EarnedTier(EarnedCombatState.Untouchable);
        if (currentUntouchableTier > previousUntouchableTier)
        {
            progressOutcome = previousUntouchableTier == 0
                ? UntouchableProgressOutcome.TierActivated
                : UntouchableProgressOutcome.TierEscalated;
        }

        facts.Publish(
            new UntouchableProgress(
                context,
                source,
                defense,
                serverSequence,
                previousStreak,
                UntouchableStreak,
                previousUntouchableTier,
                currentUntouchableTier,
                progressOutcome));
    }

    internal void Observe(AcceptedPlayerDamage damage)
    {
        if (damage.After.Player != player || damage.HealthLost <= 0f)
        {
            return;
        }

        int previousStreak = UntouchableStreak;
        int previousTier = EarnedTier(EarnedCombatState.Untouchable);
        if (previousStreak == 0 && previousTier == 0)
        {
            return;
        }

        UntouchableStreak = 0;
        facts.Publish(
            new UntouchableReset(
                damage,
                previousStreak,
                previousTier,
                UntouchableResetReason.AcceptedDamage));
        Deactivate(
            EarnedCombatState.Untouchable,
            damage.After,
            EarnedStateTransitionReason.AcceptedDamage);
    }

    internal void Observe(BerserkerChainTransition transition)
    {
        if (transition.Context.Player != player)
        {
            return;
        }

        float remainingDuration = transition.RemainingDurationSeconds(
            ZNet.instance?.GetTimeSeconds() ?? transition.ServerTimeSeconds);
        bool activeTransition = transition.Kind == BerserkerChainTransitionKind.Activated
            || transition.Kind == BerserkerChainTransitionKind.Refreshed
            || transition.Kind == BerserkerChainTransitionKind.Escalated;
        if (activeTransition && remainingDuration <= 0f)
        {
            Deactivate(
                EarnedCombatState.Berserker,
                transition.Context,
                EarnedStateTransitionReason.ServerChainExpired);
            facts.Publish(
                new EarnedStateTransition(
                    transition.Context,
                    EarnedCombatState.Berserker,
                    BerserkerMechanic.TierNumber(transition.Tier),
                    EarnedStateTransitionKind.Rejected,
                    EarnedStateTransitionReason.ServerChainAlreadyExpired));
            return;
        }

        switch (transition.Kind)
        {
            case BerserkerChainTransitionKind.Activated:
                Earn(
                    transition.Context,
                    EarnedCombatState.Berserker,
                    BerserkerMechanic.TierNumber(transition.Tier),
                    EarnedStateTransitionKind.Activated,
                    remainingDuration);
                break;
            case BerserkerChainTransitionKind.Refreshed:
                Earn(
                    transition.Context,
                    EarnedCombatState.Berserker,
                    BerserkerMechanic.TierNumber(transition.Tier),
                    EarnedStateTransitionKind.Refreshed,
                    remainingDuration);
                break;
            case BerserkerChainTransitionKind.Escalated:
                Earn(
                    transition.Context,
                    EarnedCombatState.Berserker,
                    BerserkerMechanic.TierNumber(transition.Tier),
                    EarnedStateTransitionKind.Activated,
                    remainingDuration);
                break;
            case BerserkerChainTransitionKind.Expired:
                Deactivate(
                    EarnedCombatState.Berserker,
                    transition.Context,
                    EarnedStateTransitionReason.ServerChainExpired);
                break;
        }
    }

    internal bool Earn(
        PlayerCombatContext context,
        EarnedCombatState state,
        int tier,
        EarnedStateTransitionKind? acceptedKind = null,
        float? durationSeconds = null)
    {
        if (context.Player != player)
        {
            throw new ArgumentException("Earned-state context must identify the controller player.");
        }

        if (tier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tier));
        }

        bool replacingTier = activeStates.TryGetValue(state, out int currentTier)
            && currentTier != tier;

        EarnedStateOutputResult result = output.Activate(
            player,
            state,
            tier,
            durationSeconds);
        EarnedStateTransitionKind transitionKind;
        switch (result.Outcome)
        {
            case EarnedStateOutputOutcome.Activated:
                transitionKind = acceptedKind ?? EarnedStateTransitionKind.Activated;
                break;
            case EarnedStateOutputOutcome.Refreshed:
                transitionKind = acceptedKind ?? EarnedStateTransitionKind.Refreshed;
                break;
            default:
                transitionKind = EarnedStateTransitionKind.Rejected;
                break;
        }

        if (result.Outcome != EarnedStateOutputOutcome.Rejected)
        {
            if (replacingTier)
            {
                Deactivate(
                    state,
                    context,
                    EarnedStateTransitionReason.TierReplaced);
            }

            activeStates[state] = tier;
        }

        facts.Publish(
            new EarnedStateTransition(
                context,
                state,
                tier,
                transitionKind,
                result.Reason));
        return result.Outcome != EarnedStateOutputOutcome.Rejected;
    }

    internal bool HasEarned(EarnedCombatState state)
    {
        return activeStates.ContainsKey(state);
    }

    internal int EarnedTier(EarnedCombatState state)
    {
        return activeStates.TryGetValue(state, out int tier) ? tier : 0;
    }

    internal bool ForgetStoppedOutput(EarnedCombatState state, int tier)
    {
        if (activeStates.TryGetValue(state, out int currentTier) && currentTier == tier)
        {
            activeStates.Remove(state);
            return true;
        }

        return false;
    }

    internal void Reset()
    {
        UntouchableStreak = 0;
        // Keep cleanup order stable so native stop effects and diagnostics do
        // not depend on dictionary enumeration.
        PlayerCombatContext context = PlayerCombatContext.Capture(player);
        Deactivate(
            EarnedCombatState.Clutch,
            context,
            EarnedStateTransitionReason.LifecycleReset);
        Deactivate(
            EarnedCombatState.Untouchable,
            context,
            EarnedStateTransitionReason.LifecycleReset);
        Deactivate(
            EarnedCombatState.Berserker,
            context,
            EarnedStateTransitionReason.LifecycleReset);
    }

    private void Deactivate(
        EarnedCombatState state,
        PlayerCombatContext context,
        EarnedStateTransitionReason reason)
    {
        if (!activeStates.TryGetValue(state, out int tier))
        {
            return;
        }

        try
        {
            output.Deactivate(player, state, tier);
        }
        finally
        {
            activeStates.Remove(state);
        }

        facts.Publish(
            new EarnedStateTransition(
                context,
                state,
                tier,
                EarnedStateTransitionKind.Removed,
                reason));
    }
}
