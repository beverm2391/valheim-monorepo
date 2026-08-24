using System.Reflection;
using BenheimQoL.Adrenaline;
using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.PlayerCombat;

/// <summary>
/// Holds the short native call context needed to distinguish a confirmed
/// perfect-defense adrenaline callback from every other adrenaline grant.
/// </summary>
internal static class PerfectDefenseObservation
{
    private static readonly FieldInfo BlockTimerField =
        AccessTools.Field(typeof(Humanoid), "m_blockTimer");
    private static readonly FieldInfo LeftItemField =
        AccessTools.Field(typeof(Humanoid), "m_leftItem");
    private static readonly FieldInfo BeenHitWhileDodgingField =
        AccessTools.Field(typeof(Player), "m_beenHitWhileDodging");
    private static readonly FieldInfo CurrentAttackField =
        AccessTools.Field(typeof(Humanoid), "m_currentAttack");
    private static readonly NativeAttackOutcomeIdentities<Attack> AttackOutcomes =
        new NativeAttackOutcomeIdentities<Attack>();
    private static readonly PerfectDefenseOutcomeDeduplicator ConfirmedOutcomes =
        new PerfectDefenseOutcomeDeduplicator();

    private static Candidate? candidate;

    internal static void BeginParry(Humanoid defender, HitData hit, Character attacker)
    {
        End();
        if (defender != Player.m_localPlayer || !attacker)
        {
            return;
        }

        float blockTimer = (float)BlockTimerField.GetValue(defender);
        ItemDrop.ItemData? blocker = (ItemDrop.ItemData?)LeftItemField.GetValue(defender)
            ?? defender.GetCurrentWeapon();
        if (blocker == null
            || blocker.m_shared.m_timedBlockBonus <= 1f
            || blockTimer < 0f
            || blockTimer >= 0.25f)
        {
            return;
        }

        OutcomeIdentity outcome = ResolveParryOutcomeIdentity(hit, attacker);
        candidate = new Candidate(
            PlayerCombatContext.Capture((Player)defender),
            PerfectDefenseKind.Parry,
            blockTimer,
            blocker.m_shared.m_timedBlockBonus,
            outcome.Identity,
            outcome.Source);
    }

    internal static void BeginDodge(Player player)
    {
        End();
        bool alreadyAwarded = (bool)BeenHitWhileDodgingField.GetValue(player);
        if (player == Player.m_localPlayer && !alreadyAwarded)
        {
            candidate = new Candidate(
                PlayerCombatContext.Capture(player),
                PerfectDefenseKind.Dodge,
                blockTimer: null,
                timedBlockBonus: null,
                outcomeIdentity: new object(),
                outcomeSource: "native_dodge_rpc");
        }
    }

    internal static PerfectDefenseConfirmation ConfirmFromNativeAdrenaline(Player player)
    {
        Candidate? current = candidate;
        if (current == null || current.Confirmed || current.Context.Player != player)
        {
            return PerfectDefenseConfirmation.None;
        }

        current.Confirmed = true;
        if (!ConfirmedOutcomes.TryAccept(current.OutcomeIdentity, out int outcomeToken))
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("PlayerCombat", "perfect_defense_rejected")
                    .String("defense", DefenseName(current.Kind))
                    .String("status", "rejected")
                    .String("reason", "duplicate_native_outcome")
                    .String("outcome_source", current.OutcomeSource)
                    .Integer("outcome_token", outcomeToken));
            return PerfectDefenseConfirmation.DuplicateNativeOutcome;
        }

        PlayerCombatRuntime.BeginPerfectDefensePresentation(current.Context);
        PlayerCombatRuntime.Publish(
            new PerfectDefenseConfirmed(
                current.Context,
                current.Kind,
                current.BlockTimer,
                current.TimedBlockBonus,
                current.OutcomeSource,
                outcomeToken));
        return PerfectDefenseConfirmation.Accepted;
    }

    internal static void End()
    {
        candidate = null;
        AdrenalineFeedback.EndPerfectDefense();
    }

    internal static void Reset()
    {
        End();
        AttackOutcomes.Reset();
        ConfirmedOutcomes.Reset();
    }

    internal static void BeginNativeAttackOutcome(Attack attack)
    {
        AttackOutcomes.Begin(attack);
    }

    private static OutcomeIdentity ResolveParryOutcomeIdentity(
        HitData hit,
        Character attacker)
    {
        // Projectiles and AOEs can arrive after the attacker starts another
        // attack. Valheim marks those hits directly, so never infer their
        // identity from the attacker's current melee state.
        if (hit.m_ranged)
        {
            return new OutcomeIdentity(hit, "ranged_hit");
        }

        if (attacker is Humanoid
            && CurrentAttackField.GetValue(attacker) is Attack attack)
        {
            // Ordinary melee uses one cloned Attack per swing; looping melee
            // advances once per native attack trigger.
            if (!attack.m_loopingAttack)
            {
                return new OutcomeIdentity(attack, "attacker_attack");
            }

            object identity = AttackOutcomes.GetOrBegin(
                attack,
                out bool triggerObserved);
            return new OutcomeIdentity(
                identity,
                triggerObserved
                    ? "looping_attack_trigger"
                    : "looping_attack");
        }

        return new OutcomeIdentity(hit, "hit_data");
    }

    private static string DefenseName(PerfectDefenseKind kind) =>
        kind == PerfectDefenseKind.Parry ? "parry" : "dodge";

    private readonly struct OutcomeIdentity
    {
        internal OutcomeIdentity(object identity, string source)
        {
            Identity = identity;
            Source = source;
        }

        internal object Identity { get; }
        internal string Source { get; }
    }

    private sealed class Candidate
    {
        internal Candidate(
            PlayerCombatContext context,
            PerfectDefenseKind kind,
            float? blockTimer,
            float? timedBlockBonus,
            object outcomeIdentity,
            string outcomeSource)
        {
            Context = context;
            Kind = kind;
            BlockTimer = blockTimer;
            TimedBlockBonus = timedBlockBonus;
            OutcomeIdentity = outcomeIdentity;
            OutcomeSource = outcomeSource;
        }

        internal PlayerCombatContext Context { get; }
        internal PerfectDefenseKind Kind { get; }
        internal float? BlockTimer { get; }
        internal float? TimedBlockBonus { get; }
        internal object OutcomeIdentity { get; }
        internal string OutcomeSource { get; }
        internal bool Confirmed { get; set; }
    }
}

internal enum PerfectDefenseConfirmation
{
    None,
    Accepted,
    DuplicateNativeOutcome
}

[HarmonyPatch(typeof(Attack), nameof(Attack.OnAttackTrigger))]
internal static class NativeAttackOutcomePatch
{
    private static void Prefix(Attack __instance)
    {
        if (__instance.m_loopingAttack)
        {
            PerfectDefenseObservation.BeginNativeAttackOutcome(__instance);
        }
    }
}
