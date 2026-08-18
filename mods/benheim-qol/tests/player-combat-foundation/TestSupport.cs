using System;
using System.Collections.Generic;
using System.Reflection;
using BenheimQoL.Infrastructure;
using BenheimQoL.PlayerCombat;
using UnityEngine;
using static TestSupport;

internal static class SupportTests
{
    internal static void TestOrderedFailureIsolationAndReset()
    {
        List<string> calls = new List<string>();
        int failures = 0;
        LocalGameEventBus events = new LocalGameEventBus((type, exception) =>
        {
            Expect(type == typeof(TestFact), "failure reports the published event type");
            Expect(exception.Message == "expected", "failure reports the subscriber exception");
            failures++;
        });

        events.Subscribe<TestFact>(_ => calls.Add("first"));
        events.Subscribe<TestFact>(_ => throw new InvalidOperationException("expected"));
        IDisposable last = events.Subscribe<TestFact>(_ => calls.Add("last"));
        events.Publish(new TestFact());
        Expect(string.Join(",", calls) == "first,last", "later subscribers run after one fails");
        Expect(failures == 1, "one failed subscriber is reported once");

        last.Dispose();
        events.Publish(new TestFact());
        Expect(string.Join(",", calls) == "first,last,first", "disposed subscriber stays removed");
        events.Reset();
        events.Publish(new TestFact());
        Expect(string.Join(",", calls) == "first,last,first", "reset removes every subscriber");
    }

    internal static void TestClutchDecisionRefreshAndDamageLifecycle()
    {
        Player player = new Player(30f, 100f);
        FakeOutput output = new FakeOutput();
        FactRecorder facts = new FactRecorder();
        PlayerCombatController controller = new PlayerCombatController(player, output, facts);

        controller.Observe(Defense(player, PerfectDefenseKind.Parry));
        Expect(facts.Clutch[^1].Outcome == ClutchDecisionOutcome.Reject,
            "exactly 30 health rejects CLUTCH");
        Expect(output.Activations.Count == 0, "rejected CLUTCH has no native output");

        player.Health = 29.99f;
        controller.Observe(Defense(player, PerfectDefenseKind.Dodge));
        Expect(facts.Clutch[^1].Outcome == ClutchDecisionOutcome.Activate,
            "strictly below 30 health activates CLUTCH");
        Expect(controller.HasEarned(EarnedCombatState.Clutch), "CLUTCH is tracked as active");
        Expect(facts.Transitions[^1].Kind == EarnedStateTransitionKind.Activated,
            "native entry produces an activation fact");

        controller.Observe(Defense(player, PerfectDefenseKind.Parry));
        Expect(facts.Clutch[^1].Outcome == ClutchDecisionOutcome.Refresh,
            "eligible retrigger decides to refresh");
        Expect(facts.Transitions[^1].Kind == EarnedStateTransitionKind.Refreshed,
            "one active output is refreshed");

        PlayerCombatContext before = PlayerCombatContext.Capture(player);
        player.Health = 20f;
        controller.Observe(new AcceptedPlayerDamage(before, PlayerCombatContext.Capture(player)));
        Expect(controller.HasEarned(EarnedCombatState.Clutch),
            "accepted damage does not cancel CLUTCH");
        controller.Reset();
        Expect(!controller.HasEarned(EarnedCombatState.Clutch),
            "lifecycle reset clears CLUTCH");
    }

    internal static void TestConfirmedKillFactsAreImmutable()
    {
        Player player = new Player(40f, 100f);
        ConfirmedKill confirmedKill = new ConfirmedKill(
            PlayerCombatContext.Capture(player),
            new ZDOID(1),
            new ZDOID(2),
            "Troll",
            123,
            3,
            victimWasBoss: false,
            victimWasTamed: false,
            new Vector3(4f, 5f, 6f),
            serverSequence: 7,
            serverTimeSeconds: 8.5);

        Expect(confirmedKill.ServerSequence == 7, "server sequence stays on the gameplay fact");
        Expect(confirmedKill.ServerTimeSeconds == 8.5, "server time stays on the gameplay fact");
        foreach (PropertyInfo property in typeof(ConfirmedKill).GetProperties(
            BindingFlags.Instance | BindingFlags.NonPublic))
        {
            Expect(!property.CanWrite, $"{property.Name} is immutable");
        }
    }
}

internal static class TestSupport
{
    internal static ObjectDB CreateNativeIconDatabase()
    {
        ObjectDB database = new ObjectDB();
        StatusEffect healing = new StatusEffect
        {
            name = ClutchMechanic.HealthIconStatusEffect,
            m_icon = new Sprite { name = "healing" }
        };
        ItemDrop mead = new ItemDrop();
        mead.m_itemData.m_shared.m_consumeStatusEffect = healing;
        database.AddItem(ClutchMechanic.HealthIconItemPrefab, mead);

        StatusEffect damageCharm = new StatusEffect
        {
            name = UntouchableMechanic.DamageIconStatusEffect,
            m_icon = new Sprite { name = "wolf-sight" }
        };
        ItemDrop charm = new ItemDrop();
        charm.m_itemData.m_shared.m_fullAdrenalineSE = damageCharm;
        database.AddItem(UntouchableMechanic.DamageIconItemPrefab, charm);

        StatusEffect resistanceCharm = new StatusEffect
        {
            name = BerserkerMechanic.ResistanceIconStatusEffect,
            m_icon = new Sprite { name = "crystal-heart" }
        };
        ItemDrop crystalHeart = new ItemDrop();
        crystalHeart.m_itemData.m_shared.m_fullAdrenalineSE = resistanceCharm;
        database.AddItem(BerserkerMechanic.ResistanceIconItemPrefab, crystalHeart);
        return database;
    }

    internal static PerfectDefenseConfirmed Defense(Player player, PerfectDefenseKind kind) =>
        new PerfectDefenseConfirmed(PlayerCombatContext.Capture(player), kind);

    internal static EarnedStateTransition Transition(
        PlayerCombatContext context,
        EarnedCombatState state,
        int tier,
        EarnedStateTransitionKind kind) =>
        new EarnedStateTransition(
            context,
            state,
            tier,
            kind,
            kind == EarnedStateTransitionKind.Refreshed
                ? EarnedStateTransitionReason.NativeEffectRefreshed
                : EarnedStateTransitionReason.NativeEffectApplied);

    internal static BerserkerChainTransition BerserkerTransition(
        Player player,
        BerserkerChainTransitionKind kind,
        BerserkerChainTier tier,
        int killCount)
    {
        bool terminal = kind == BerserkerChainTransitionKind.Expired;
        return new BerserkerChainTransition(
            PlayerCombatContext.Capture(player),
            kind,
            tier,
            killCount,
            serverSequence: 20,
            serverTimeSeconds: 100d,
            expiresAtServerTimeSeconds: terminal ? 0d : 110d);
    }

    internal static void Expect(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    internal static void ExpectThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}

internal sealed class TestFact
{
}

internal sealed class FakeOutput : IEarnedStateOutput
{
    private readonly Dictionary<EarnedCombatState, int> active =
        new Dictionary<EarnedCombatState, int>();
    private readonly HashSet<string> rejected = new HashSet<string>();

    internal List<string> Activations { get; } = new List<string>();
    internal List<string> Deactivations { get; } = new List<string>();
    internal float? LastDurationSeconds { get; private set; }

    public EarnedStateOutputResult Activate(
        Player player,
        EarnedCombatState state,
        int tier,
        float? durationSeconds = null)
    {
        Activations.Add($"{state}:{tier}");
        LastDurationSeconds = durationSeconds;
        if (rejected.Contains($"{state}:{tier}"))
        {
            return EarnedStateOutputResult.Rejected(
                EarnedStateTransitionReason.EffectUnavailable);
        }

        if (active.TryGetValue(state, out int current) && current == tier)
        {
            return EarnedStateOutputResult.Refreshed();
        }

        active[state] = tier;
        return EarnedStateOutputResult.Activated();
    }

    public void Deactivate(Player player, EarnedCombatState state, int tier)
    {
        Deactivations.Add($"{state}:{tier}");
        if (active.TryGetValue(state, out int currentTier) && currentTier == tier)
        {
            active.Remove(state);
        }
    }

    internal int ActiveTier(EarnedCombatState state) =>
        active.TryGetValue(state, out int tier) ? tier : 0;

    internal void Reject(EarnedCombatState state, int tier) =>
        rejected.Add($"{state}:{tier}");
}

internal sealed class FactRecorder : IPlayerCombatFactPublisher
{
    internal List<ClutchDecision> Clutch { get; } = new List<ClutchDecision>();
    internal List<UntouchableProgress> Untouchable { get; } = new List<UntouchableProgress>();
    internal List<UntouchableReset> Resets { get; } = new List<UntouchableReset>();
    internal List<EarnedStateTransition> Transitions { get; } = new List<EarnedStateTransition>();

    public void Publish(ClutchDecision decision) => Clutch.Add(decision);
    public void Publish(UntouchableProgress progress) => Untouchable.Add(progress);
    public void Publish(UntouchableReset reset) => Resets.Add(reset);
    public void Publish(EarnedStateTransition transition) => Transitions.Add(transition);
}
