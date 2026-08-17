using System;
using System.Collections.Generic;
using System.Reflection;
using BenheimQoL.Infrastructure;
using BenheimQoL.PlayerCombat;
using UnityEngine;

TestOrderedFailureIsolationAndReset();
TestControllerLifecycle();
TestNativeEffectRegistrationAndOutput();
TestConfirmedKillFactsAreImmutable();

Console.WriteLine("player combat event, controller, and lifecycle checks passed");

static void TestOrderedFailureIsolationAndReset()
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

static void TestControllerLifecycle()
{
    Player player = new Player(80f, 100f);
    FakeOutput output = new FakeOutput();
    PlayerCombatController controller = new PlayerCombatController(player, output);

    controller.Observe(
        new PerfectDefenseConfirmed(
            PlayerCombatContext.Capture(player),
            PerfectDefenseKind.Parry,
            blockTimer: 0.1f,
            timedBlockBonus: 1.5f));
    controller.Observe(
        new PerfectDefenseConfirmed(
            PlayerCombatContext.Capture(player),
            PerfectDefenseKind.Dodge));
    Expect(controller.ConsecutivePerfectDefenses == 2, "confirmed defenses build the streak");

    Expect(controller.Earn(EarnedCombatState.Untouchable, 1), "untouchable output activates");
    Expect(controller.Earn(EarnedCombatState.Clutch, 1), "clutch output activates");
    Expect(controller.HasEarned(EarnedCombatState.Untouchable), "untouchable is tracked");

    PlayerCombatContext before = PlayerCombatContext.Capture(player);
    player.Health = 65f;
    controller.Observe(new AcceptedPlayerDamage(before, PlayerCombatContext.Capture(player)));
    Expect(controller.ConsecutivePerfectDefenses == 0, "accepted damage clears the streak");
    Expect(!controller.HasEarned(EarnedCombatState.Untouchable), "accepted damage ends untouchable");
    Expect(controller.HasEarned(EarnedCombatState.Clutch), "accepted damage does not invent clutch expiry");

    controller.Reset();
    Expect(!controller.HasEarned(EarnedCombatState.Clutch), "lifecycle reset clears clutch");
    Expect(output.Deactivations.Count == 2, "each active native output is removed once");

    output.AllowActivation = false;
    Expect(!controller.Earn(EarnedCombatState.Berserker, 1), "failed native output rejects earning");
    Expect(!controller.HasEarned(EarnedCombatState.Berserker), "failed output is not tracked as active");
}

static void TestConfirmedKillFactsAreImmutable()
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

static void TestNativeEffectRegistrationAndOutput()
{
    ObjectDB database = new ObjectDB();
    EarnedStateStatusEffect clutch = new EarnedStateStatusEffect
    {
        name = "SE_Benheim_Clutch"
    };
    EarnedStateEffectCatalog catalog = new EarnedStateEffectCatalog();
    catalog.Configure(
        new EarnedStateEffectDefinition(
            EarnedCombatState.Clutch,
            1,
            clutch,
            "CLUTCH"));

    catalog.Register(database);
    catalog.Register(database);
    Expect(database.m_StatusEffects.Count == 1, "native registration is duplicate-safe");

    MessageHud.instance = new MessageHud();
    Player player = new Player(20f, 100f);
    NativeEarnedStateOutput output = new NativeEarnedStateOutput(
        catalog,
        new EarnedStatePresentation());
    Expect(output.Activate(player, EarnedCombatState.Clutch, 1), "registered native output activates");
    Expect(player.GetSEMan().HaveStatusEffect(clutch.NameHash()), "native effect is applied by hash");
    Expect(MessageHud.instance.LastBanner == "CLUTCH", "shared presenter uses the activation copy");

    output.Deactivate(player, EarnedCombatState.Clutch, 1);
    Expect(!player.GetSEMan().HaveStatusEffect(clutch.NameHash()), "native output removes by hash");

    clutch.m_character = player;
    clutch.Stop();
    Expect(PlayerCombatRuntime.StoppedEffects == 1, "native expiry closes controller lifecycle");

    catalog.Unregister();
    Expect(database.m_StatusEffects.Count == 0, "plugin teardown unregisters owned templates");
}

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class TestFact
{
}

internal sealed class FakeOutput : IEarnedStateOutput
{
    internal bool AllowActivation { get; set; } = true;
    internal List<string> Deactivations { get; } = new List<string>();

    public bool Activate(Player player, EarnedCombatState state, int tier)
    {
        return AllowActivation;
    }

    public void Deactivate(Player player, EarnedCombatState state, int tier)
    {
        Deactivations.Add($"{state}:{tier}");
    }
}
