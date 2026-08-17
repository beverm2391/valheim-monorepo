using System.Reflection;
using BenheimQoL.Adrenaline;
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

    private static Candidate? candidate;

    internal static void BeginParry(Humanoid defender, Character attacker)
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

        candidate = new Candidate(
            PlayerCombatContext.Capture((Player)defender),
            PerfectDefenseKind.Parry,
            blockTimer,
            blocker.m_shared.m_timedBlockBonus);
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
                timedBlockBonus: null);
        }
    }

    internal static void ConfirmFromNativeAdrenaline(Player player)
    {
        Candidate? current = candidate;
        if (current == null || current.Confirmed || current.Context.Player != player)
        {
            return;
        }

        current.Confirmed = true;
        PlayerCombatRuntime.Publish(
            new PerfectDefenseConfirmed(
                current.Context,
                current.Kind,
                current.BlockTimer,
                current.TimedBlockBonus));
    }

    internal static void End()
    {
        candidate = null;
        AdrenalineFeedback.EndPerfectDefense();
    }

    private sealed class Candidate
    {
        internal Candidate(
            PlayerCombatContext context,
            PerfectDefenseKind kind,
            float? blockTimer,
            float? timedBlockBonus)
        {
            Context = context;
            Kind = kind;
            BlockTimer = blockTimer;
            TimedBlockBonus = timedBlockBonus;
        }

        internal PlayerCombatContext Context { get; }
        internal PerfectDefenseKind Kind { get; }
        internal float? BlockTimer { get; }
        internal float? TimedBlockBonus { get; }
        internal bool Confirmed { get; set; }
    }
}
