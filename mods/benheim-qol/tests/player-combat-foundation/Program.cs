using System;
using System.Collections.Generic;
using System.Reflection;
using BenheimQoL.Adrenaline;
using BenheimQoL.Infrastructure;
using BenheimQoL.PlayerCombat;
using UnityEngine;
using static TestSupport;

SupportTests.TestOrderedFailureIsolationAndReset();
SupportTests.TestClutchDecisionRefreshAndDamageLifecycle();
PerfectDefenseOutcomeIdentityTests.Run();
TestUntouchableMixedDefenseTiersAndDamageReset();
SupportTests.TestHealthLossWithoutUntouchableStateIsNotAReset();
TestRejectedUntouchableEscalationKeepsPriorTier();
TestBerserkerConsumesAuthoritativeChainTransitions();
TestExpiredBerserkerTransitionClearsPriorOutput();
TestNativeEffectsRegistrationHealingAndReplacement();
TestMissingNativeIconRejectsRegistration();
TestEntryPresentationAndPerDefenseCharmCoalescing();
TestNativeCharmActivationSuppressesDuplicateEarnedStateCue();
TestBerserkerTransitionValidation();
SupportTests.TestConfirmedKillFactsAreImmutable();
Console.WriteLine("player combat earned-state checks passed");
static void TestUntouchableMixedDefenseTiersAndDamageReset()
{
    Player player = new Player(80f, 100f);
    FakeOutput output = new FakeOutput();
    FactRecorder facts = new FactRecorder();
    PlayerCombatController controller = new PlayerCombatController(player, output, facts);

    for (int defense = 1; defense <= 12; defense++)
    {
        controller.Observe(
            Defense(
                player,
                defense % 2 == 0 ? PerfectDefenseKind.Dodge : PerfectDefenseKind.Parry));

        int expectedTier = defense >= 12 ? 3 : defense >= 8 ? 2 : defense >= 5 ? 1 : 0;
        Expect(controller.ConsecutivePerfectDefenses == defense,
            $"defense {defense} increments the shared streak");
        Expect(controller.EarnedTier(EarnedCombatState.Untouchable) == expectedTier,
            $"defense {defense} selects only the approved tier");
    }

    Expect(output.ActiveTier(EarnedCombatState.Untouchable) == 3,
        "tier replacement leaves one active UNTOUCHABLE modifier");
    Expect(facts.Untouchable[4].Outcome == UntouchableProgressOutcome.TierActivated,
        "the fifth defense activates Tier I");
    Expect(facts.Untouchable[7].Outcome == UntouchableProgressOutcome.TierEscalated,
        "the eighth defense escalates Tier II");
    Expect(facts.Untouchable[11].Outcome == UntouchableProgressOutcome.TierEscalated,
        "the twelfth defense escalates Tier III");

    PlayerCombatContext before = PlayerCombatContext.Capture(player);
    player.Health = 79f;
    controller.Observe(new AcceptedPlayerDamage(before, PlayerCombatContext.Capture(player)));
    Expect(controller.ConsecutivePerfectDefenses == 0, "accepted health loss resets the streak");
    Expect(!controller.HasEarned(EarnedCombatState.Untouchable),
        "accepted health loss quietly removes the active tier");
    Expect(facts.Resets[^1].Damage.HealthLost == 1f,
        "reset fact retains accepted health loss");

    controller.Observe(Defense(player, PerfectDefenseKind.Dodge));
    PlayerCombatContext unchanged = PlayerCombatContext.Capture(player);
    int resetsBefore = facts.Resets.Count;
    controller.Observe(new AcceptedPlayerDamage(unchanged, PlayerCombatContext.Capture(player)));
    Expect(controller.ConsecutivePerfectDefenses == 1,
        "zero-health-loss contact does not reset the streak");
    Expect(facts.Resets.Count == resetsBefore,
        "zero-health-loss contact emits no reset fact");
}

static void TestRejectedUntouchableEscalationKeepsPriorTier()
{
    Player player = new Player(80f, 100f);
    FakeOutput output = new FakeOutput();
    FactRecorder facts = new FactRecorder();
    PlayerCombatController controller = new PlayerCombatController(player, output, facts);

    for (int defense = 1; defense <= 7; defense++)
    {
        controller.Observe(Defense(player, PerfectDefenseKind.Parry));
    }

    output.Reject(EarnedCombatState.Untouchable, 2);
    controller.Observe(Defense(player, PerfectDefenseKind.Dodge));

    Expect(controller.EarnedTier(EarnedCombatState.Untouchable) == 1,
        "rejected UNTOUCHABLE escalation retains the working prior tier");
    Expect(facts.Untouchable[^1].CurrentTier == 1
            && facts.Untouchable[^1].Outcome == UntouchableProgressOutcome.StreakIncremented,
        "UNTOUCHABLE progress reports actual state after rejected output");
    Expect(facts.Transitions[^1].Kind == EarnedStateTransitionKind.Rejected,
        "rejected UNTOUCHABLE escalation emits the shared rejection fact");
}

static void TestNativeEffectsRegistrationHealingAndReplacement()
{
    ObjectDB database = CreateNativeIconDatabase();
    EarnedStateEffectDefinition clutch = ClutchMechanic.CreateEffectDefinition();
    EarnedStateEffectDefinition untouchable1 = UntouchableMechanic.CreateEffectDefinition(1);
    EarnedStateEffectDefinition untouchable2 = UntouchableMechanic.CreateEffectDefinition(2);
    EarnedStateEffectDefinition untouchable3 = UntouchableMechanic.CreateEffectDefinition(3);
    EarnedStateEffectDefinition berserker1 = BerserkerMechanic.CreateEffectDefinition(1);
    EarnedStateEffectDefinition berserker2 = BerserkerMechanic.CreateEffectDefinition(2);
    EarnedStateEffectCatalog catalog = new EarnedStateEffectCatalog();
    catalog.Configure(
        clutch,
        untouchable1,
        untouchable2,
        untouchable3,
        berserker1,
        berserker2);
    catalog.Register(database);
    catalog.Register(database);

    Expect(database.m_StatusEffects.Count == 6, "registration is duplicate-safe");
    Expect(clutch.Effect.m_icon != null, "CLUTCH copies the lingering-healing icon Sprite");
    Expect(clutch.Effect.m_ttl == 6f, "CLUTCH lasts six seconds");
    Expect(clutch.Effect.m_tickInterval == 1f && clutch.Effect.m_healthPerTick == 10f,
        "CLUTCH heals ten health each second");
    Expect(untouchable3.Effect.m_ttl == 0f,
        "UNTOUCHABLE uses native indefinite status presentation without a timer");
    Expect(untouchable3.Effect.m_modifyAttackSkill == Skills.SkillType.All,
        "UNTOUCHABLE modifies all outgoing attack skills");
    Expect(Math.Abs(untouchable3.Effect.m_damageModifier - 1.30f) < 0.001f,
        "UNTOUCHABLE Tier III adds thirty percent damage");
    Expect(clutch.Effect.m_category != untouchable1.Effect.m_category,
        "earned states use non-conflicting native categories");
    Expect(berserker1.Effect.m_mods.Count == 3,
        "BERSERKER configures only blunt, slash, and pierce resistance");
    Expect(berserker1.Effect.m_mods.TrueForAll(
            pair => pair.m_modifier == HitData.DamageModifier.SlightlyResistant),
        "BERSERKER uses native SlightlyResistant");
    Expect(berserker2.Effect.m_mods.TrueForAll(
            pair => pair.m_modifier == HitData.DamageModifier.Resistant),
        "SLAUGHTERHOUSE uses native Resistant");
    Expect(berserker1.Effect.m_staminaRegenMultiplier == 1.5f
            && berserker2.Effect.m_staminaRegenMultiplier == 2f,
        "Berserker tiers use approved native stamina regeneration multipliers");

    Sprite firstUntouchableIcon = untouchable1.Effect.m_icon
        ?? throw new InvalidOperationException("UNTOUCHABLE icon was not registered");
    ObjectDB currentDatabase = CreateNativeIconDatabase();
    catalog.Register(currentDatabase);
    Expect(database.m_StatusEffects.Count == 0
            && currentDatabase.m_StatusEffects.Count == 6,
        "ObjectDB replacement unregisters old templates and registers the populated current database");
    Expect(untouchable1.Effect.m_icon != firstUntouchableIcon,
        "registration rebinds the icon from the current ObjectDB instead of retaining an earlier lifecycle donor");
    PlayerCombatRuntime.ResetStops();
    Player player = new Player(20f, 70f);
    NativeEarnedStateOutput output = new NativeEarnedStateOutput(catalog);
    Expect(output.Activate(player, EarnedCombatState.Clutch, 1).Outcome
            == EarnedStateOutputOutcome.Activated,
        "registered CLUTCH activates through SEMan by hash");
    for (int second = 0; second < 6; second++)
    {
        player.GetSEMan().Tick(1.01f);
    }
    Expect(player.Health == 70f, "native healing is capped by maximum health");
    Expect(PlayerCombatRuntime.ExpiredEffects == 1, "native duration reports CLUTCH expiry");
    Player refreshPlayer = new Player(20f, 200f);
    output.Activate(refreshPlayer, EarnedCombatState.Clutch, 1);
    refreshPlayer.GetSEMan().Tick(1.01f);
    Expect(output.Activate(refreshPlayer, EarnedCombatState.Clutch, 1).Outcome
            == EarnedStateOutputOutcome.Refreshed,
        "retrigger refreshes the same native effect");
    Expect(refreshPlayer.GetSEMan().Count == 1,
        "CLUTCH refresh does not duplicate the effect or icon");
    for (int second = 0; second < 6; second++)
    {
        refreshPlayer.GetSEMan().Tick(1.01f);
    }
    Expect(refreshPlayer.Health == 90f,
        "CLUTCH refresh restarts one complete sixty-health recovery window");
    output.Deactivate(refreshPlayer, EarnedCombatState.Clutch, 1);
    Expect(output.Activate(player, EarnedCombatState.Untouchable, 1).Outcome
            == EarnedStateOutputOutcome.Activated,
        "UNTOUCHABLE activates only after native status and HUD presence are established");
    List<StatusEffect> visibleEffects = new List<StatusEffect>();
    player.GetSEMan().GetHUDStatusEffects(visibleEffects);
    Expect(visibleEffects.Count == 1
            && visibleEffects[0].m_icon != null
            && visibleEffects[0].m_icon!.name == "wolf-sight",
        "UNTOUCHABLE is present in Valheim's native top-bar status source");
    player.GetSEMan().Tick(60f);
    visibleEffects.Clear();
    player.GetSEMan().GetHUDStatusEffects(visibleEffects);
    Expect(player.GetSEMan().Count == 1 && visibleEffects.Count == 1,
        "UNTOUCHABLE remains active and top-bar-visible without a timer");
    output.Deactivate(player, EarnedCombatState.Untouchable, 1);
    output.Activate(player, EarnedCombatState.Untouchable, 2);
    Expect(player.GetSEMan().Count == 1,
        "UNTOUCHABLE tier replacement leaves one native effect and icon");

    output.Deactivate(player, EarnedCombatState.Untouchable, 2);
    output.Activate(player, EarnedCombatState.Berserker, 1, durationSeconds: 6.5f);
    StatusEffect? activeBerserker = player.GetSEMan().GetStatusEffect(
        berserker1.Effect.NameHash());
    Expect(activeBerserker != null && Math.Abs(activeBerserker.m_ttl - 6.5f) < 0.001f,
        "BERSERKER native countdown uses authoritative remaining duration");
    output.Deactivate(player, EarnedCombatState.Berserker, 1);
    output.Activate(player, EarnedCombatState.Berserker, 2, durationSeconds: 4f);
    Expect(player.GetSEMan().Count == 1,
        "SLAUGHTERHOUSE replacement cannot stack native modifiers or icons");

    catalog.Reset();
    Expect(currentDatabase.m_StatusEffects.Count == 0,
        "plugin teardown unregisters owned templates");
}

static void TestMissingNativeIconRejectsRegistration()
{
    ObjectDB database = new ObjectDB();
    EarnedStateEffectDefinition clutch = ClutchMechanic.CreateEffectDefinition();
    EarnedStateEffectCatalog catalog = new EarnedStateEffectCatalog();
    catalog.Configure(clutch);
    catalog.Register(database);

    Expect(database.m_StatusEffects.Count == 0,
        "a missing source-proven icon rejects custom effect registration");
    Expect(!catalog.TryGet(EarnedCombatState.Clutch, 1, out _),
        "an unregistered effect cannot control gameplay output");
    catalog.Reset();
}

static void TestBerserkerConsumesAuthoritativeChainTransitions()
{
    ZNet.instance = new ZNet { TimeSeconds = 104d };
    Player player = new Player(100f, 100f);
    FakeOutput output = new FakeOutput();
    FactRecorder facts = new FactRecorder();
    PlayerCombatController controller = new PlayerCombatController(player, output, facts);

    controller.Observe(BerserkerTransition(
        player,
        BerserkerChainTransitionKind.Progressed,
        BerserkerChainTier.None,
        killCount: 5));
    Expect(!controller.HasEarned(EarnedCombatState.Berserker),
        "server progress below Tier I has no native output");

    controller.Observe(BerserkerTransition(
        player,
        BerserkerChainTransitionKind.Activated,
        BerserkerChainTier.Berserker,
        killCount: 6));
    Expect(controller.EarnedTier(EarnedCombatState.Berserker) == 1,
        "authoritative activation applies BERSERKER Tier I");
    Expect(Math.Abs(output.LastDurationSeconds!.Value - 26f) < 0.001f,
        "native countdown subtracts delivery latency using synchronized server time");

    controller.Observe(BerserkerTransition(
        player,
        BerserkerChainTransitionKind.Refreshed,
        BerserkerChainTier.Berserker,
        killCount: 7));
    Expect(facts.Transitions[^1].Kind == EarnedStateTransitionKind.Refreshed,
        "intermediate kill refresh stays presentation-silent");

    controller.Observe(BerserkerTransition(
        player,
        BerserkerChainTransitionKind.Escalated,
        BerserkerChainTier.Slaughterhouse,
        killCount: 12));
    Expect(controller.EarnedTier(EarnedCombatState.Berserker) == 2,
        "authoritative escalation replaces Tier I with SLAUGHTERHOUSE");
    Expect(output.ActiveTier(EarnedCombatState.Berserker) == 2,
        "Berserker tiers never overlap native modifiers or icons");

    controller.Observe(BerserkerTransition(
        player,
        BerserkerChainTransitionKind.Expired,
        BerserkerChainTier.None,
        killCount: 0));
    Expect(!controller.HasEarned(EarnedCombatState.Berserker),
        "authoritative expiry quietly removes the local output");
    ZNet.instance = null;
}

static void TestExpiredBerserkerTransitionClearsPriorOutput()
{
    ZNet.instance = new ZNet { TimeSeconds = 100d };
    Player player = new Player(100f, 100f);
    FakeOutput output = new FakeOutput();
    FactRecorder facts = new FactRecorder();
    PlayerCombatController controller = new PlayerCombatController(player, output, facts);
    controller.Observe(BerserkerTransition(
        player,
        BerserkerChainTransitionKind.Activated,
        BerserkerChainTier.Berserker,
        killCount: 6));

    ZNet.instance.TimeSeconds = 131d;
    controller.Observe(BerserkerTransition(
        player,
        BerserkerChainTransitionKind.Refreshed,
        BerserkerChainTier.Berserker,
        killCount: 7));

    Expect(!controller.HasEarned(EarnedCombatState.Berserker)
            && output.ActiveTier(EarnedCombatState.Berserker) == 0,
        "an already-expired authoritative transition clears prior BERSERKER output");
    Expect(facts.Transitions[^1].Kind == EarnedStateTransitionKind.Rejected
            && facts.Transitions[^1].Reason
                == EarnedStateTransitionReason.ServerChainAlreadyExpired,
        "expired active delivery records its typed activation rejection");
    ZNet.instance = null;
}

static void TestEntryPresentationAndPerDefenseCharmCoalescing()
{
    WorldFeedback.Reset();
    Player player = new Player(20f, 100f);
    Player.m_localPlayer = player;
    player.m_adrenalinePopEffects.Available = true;
    EarnedStatePresentation presentation = new EarnedStatePresentation();
    PlayerCombatContext sharedDefense = PlayerCombatContext.Capture(player);
    presentation.BeginPerfectDefense(sharedDefense);

    presentation.Observe(Transition(
        sharedDefense,
        EarnedCombatState.Clutch,
        1,
        EarnedStateTransitionKind.Activated));
    presentation.Observe(Transition(
        sharedDefense,
        EarnedCombatState.Untouchable,
        1,
        EarnedStateTransitionKind.Activated));
    presentation.Observe(Transition(
        sharedDefense,
        EarnedCombatState.Clutch,
        1,
        EarnedStateTransitionKind.Refreshed));

    presentation.CompletePerfectDefense(
        player,
        "Perfect parry +10",
        nativeCharmActivated: false);

    Expect(WorldFeedback.Messages.Count == 1,
        "one defense emits one local Bonus world-text instance");
    Expect(WorldFeedback.Messages[0] == "Perfect parry +10\nCLUTCH!\nUNTOUCHABLE!",
        "adrenaline and state lines retain causal order while refresh emits none");
    Expect(player.m_adrenalinePopEffects.CreateCount == 1,
        "states entered by one defense share one native charm one-shot");

    presentation.Observe(Transition(
        PlayerCombatContext.Capture(player),
        EarnedCombatState.Untouchable,
        2,
        EarnedStateTransitionKind.Activated));
    Expect(WorldFeedback.Messages[^1] == "UNTOUCHABLE II!",
        "tier escalation emits its approved title");
    Expect(player.m_adrenalinePopEffects.CreateCount == 2,
        "an unrelated activation dispatch replays the one-shot");

    presentation.Observe(Transition(
        PlayerCombatContext.Capture(player),
        EarnedCombatState.Berserker,
        1,
        EarnedStateTransitionKind.Activated));
    Expect(WorldFeedback.Messages[^1] == "BERSERKER!",
        "Berserker activation uses the shared local Bonus world-text lane");

    int messagesBeforeRefresh = WorldFeedback.Messages.Count;
    int cuesBeforeRefresh = player.m_adrenalinePopEffects.CreateCount;
    presentation.Observe(Transition(
        PlayerCombatContext.Capture(player),
        EarnedCombatState.Berserker,
        1,
        EarnedStateTransitionKind.Refreshed));
    Expect(WorldFeedback.Messages.Count == messagesBeforeRefresh
            && player.m_adrenalinePopEffects.CreateCount == cuesBeforeRefresh,
        "Berserker refresh replays neither title nor charm cue");

    presentation.Observe(Transition(
        PlayerCombatContext.Capture(player),
        EarnedCombatState.Berserker,
        2,
        EarnedStateTransitionKind.Activated));
    Expect(WorldFeedback.Messages[^1] == "SLAUGHTERHOUSE!",
        "Berserker escalation uses the approved replacement title");
}

static void TestNativeCharmActivationSuppressesDuplicateEarnedStateCue()
{
    WorldFeedback.Reset();
    Player player = new Player(20f, 100f)
    {
        Adrenaline = 0f,
        MaximumAdrenaline = 100f
    };
    Player.m_localPlayer = player;
    player.m_adrenalinePopEffects.Available = true;
    EarnedStatePresentation presentation = new EarnedStatePresentation();
    PlayerCombatRuntime.Presentation = presentation;
    PlayerCombatContext defense = PlayerCombatContext.Capture(player);
    presentation.BeginPerfectDefense(defense);
    presentation.Observe(Transition(
        defense,
        EarnedCombatState.Clutch,
        1,
        EarnedStateTransitionKind.Activated));

    AdrenalineFeedback.Award award = new AdrenalineFeedback.Award(
        "Perfect parry",
        before: 90f,
        maximum: 100f)
    {
        NativeModifiedAmount = 20f
    };
    AdrenalineFeedback.ShowAward(player, award);

    Expect(WorldFeedback.Messages[^1] == "Perfect parry +10\nCLUTCH!",
        "native charm activation retains coalesced adrenaline and state text");
    Expect(player.m_adrenalinePopEffects.CreateCount == 0,
        "earned-state presentation does not duplicate Valheim's native charm cue");
    PlayerCombatRuntime.Presentation = null;
}

static void TestBerserkerTransitionValidation()
{
    Player player = new Player(100f, 100f);
    BerserkerChainTransition transition = new BerserkerChainTransition(
        PlayerCombatContext.Capture(player),
        BerserkerChainTransitionKind.Activated,
        BerserkerChainTier.Berserker,
        killCount: 6,
        serverSequence: 12,
        serverTimeSeconds: 20d,
        expiresAtServerTimeSeconds: 26.5d);
    Expect(Math.Abs(transition.RemainingDurationSeconds(20d) - 6.5f) < 0.001f,
        "server timing facts produce the native countdown safety duration");

    ExpectThrows<ArgumentException>(
        () => new BerserkerChainTransition(
            PlayerCombatContext.Capture(player),
            BerserkerChainTransitionKind.Activated,
            BerserkerChainTier.None,
            killCount: 6,
            serverSequence: 12,
            serverTimeSeconds: 20d,
            expiresAtServerTimeSeconds: 30d),
        "an activation cannot carry a missing tier");
}
