using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Adrenaline;

internal static class AdrenalineFeedback
{
    private static string? currentSource;
    private static Award? pendingAward;

    private static readonly FieldInfo BlockTimerField =
        AccessTools.Field(typeof(Humanoid), "m_blockTimer");

    private static readonly FieldInfo LeftItemField =
        AccessTools.Field(typeof(Humanoid), "m_leftItem");

    private static readonly FieldInfo BeenHitWhileDodgingField =
        AccessTools.Field(typeof(Player), "m_beenHitWhileDodging");

    internal static void BeginPerfectParry(Humanoid defender, Character attacker)
    {
        if (defender != Player.m_localPlayer || !attacker)
        {
            return;
        }

        float blockTimer = (float)BlockTimerField.GetValue(defender);
        ItemDrop.ItemData? blocker = (ItemDrop.ItemData?)LeftItemField.GetValue(defender)
            ?? defender.GetCurrentWeapon();
        if (blocker?.m_shared.m_timedBlockBonus > 1f
            && blockTimer >= 0f
            && blockTimer < 0.25f)
        {
            currentSource = "Perfect parry";
            Diagnostics.Event(
                "Adrenaline",
                "perfect_parry_detected",
                $"block_timer={blockTimer:0.###} timed_block_bonus={blocker.m_shared.m_timedBlockBonus:0.##}");
        }
    }

    internal static void BeginPerfectDodge(Player player)
    {
        bool alreadyAwarded = (bool)BeenHitWhileDodgingField.GetValue(player);
        if (player == Player.m_localPlayer && !alreadyAwarded)
        {
            currentSource = "Perfect dodge";
            Diagnostics.Event("Adrenaline", "perfect_dodge_detected");
        }
    }

    internal static void End()
    {
        currentSource = null;
    }

    internal static Award? CaptureAward(Player player, float value)
    {
        if (player != Player.m_localPlayer || currentSource == null || value <= 0f)
        {
            return null;
        }

        float before = player.GetAdrenaline();
        float maximum = player.GetMaxAdrenaline();
        Diagnostics.Event(
            "Adrenaline",
            "award_captured",
            $"source=\"{currentSource}\" requested={value:0.###} before={before:0.###} maximum={maximum:0.###}");
        return new Award(currentSource, before, maximum);
    }

    internal static void BeginModifiedAmountCapture(Award? award, SEMan statusEffects)
    {
        if (award == null)
        {
            return;
        }

        award.StatusEffects = statusEffects;
        pendingAward = award;
    }

    internal static void CaptureModifiedAmount(SEMan statusEffects, float amount)
    {
        if (pendingAward?.StatusEffects == statusEffects)
        {
            pendingAward.NativeModifiedAmount = amount;
        }
    }

    internal static void EndModifiedAmountCapture(Award? award)
    {
        if (pendingAward == award)
        {
            pendingAward = null;
        }
    }

    internal static void ShowAward(Player player, Award? award)
    {
        if (award == null)
        {
            return;
        }

        if (!award.NativeModifiedAmount.HasValue || award.Maximum <= 0f)
        {
            return;
        }

        float headroom = Mathf.Max(0f, award.Maximum - award.Before);
        float applied = Mathf.Max(0f, Mathf.Min(award.NativeModifiedAmount.Value, headroom));
        if (applied <= 0f)
        {
            return;
        }

        float after = player.GetAdrenaline();
        string text = $"{award.Source} +{applied:0.#}";
        DamageText.instance?.ShowText(
            DamageText.TextType.Bonus,
            player.transform.position + Vector3.up * 1.75f,
            text,
            player: true);
        Diagnostics.Event(
            "Adrenaline",
            "feedback_shown",
            $"source=\"{award.Source}\" amount={applied:0.###} before={award.Before:0.###} after={after:0.###}");
    }

    internal sealed class Award
    {
        internal Award(string source, float before, float maximum)
        {
            Source = source;
            Before = before;
            Maximum = maximum;
        }

        internal string Source { get; }
        internal float Before { get; }
        internal float Maximum { get; }
        internal SEMan? StatusEffects { get; set; }
        internal float? NativeModifiedAmount { get; set; }
    }
}
