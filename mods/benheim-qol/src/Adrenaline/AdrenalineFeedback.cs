using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Adrenaline;

internal static class AdrenalineFeedback
{
    private static string? currentSource;

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

        float maximum = player.GetMaxAdrenaline();
        if (maximum <= 0f)
        {
            return null;
        }

        float before = player.GetAdrenaline();
        float fill = before / maximum;
        float amount = value * Game.m_adrenalineRate;
        amount *= player.m_adrenalineGainMultiplier.Evaluate(fill);
        player.GetSEMan().ModifyAdrenaline(amount, ref amount);
        Diagnostics.Event(
            "Adrenaline",
            "award_captured",
            $"source=\"{currentSource}\" requested={value:0.###} applied={amount:0.###} before={before:0.###} maximum={maximum:0.###}");
        return new Award(currentSource, amount);
    }

    internal static void ShowAward(Player player, Award? award)
    {
        if (award == null || award.Amount <= 0f)
        {
            return;
        }

        string text = $"{award.Source} +{award.Amount:0.#}";
        DamageText.instance?.ShowText(
            DamageText.TextType.Bonus,
            player.transform.position + Vector3.up * 1.75f,
            text,
            player: true);
        Diagnostics.Event(
            "Adrenaline",
            "feedback_shown",
            $"source=\"{award.Source}\" amount={award.Amount:0.###}");
    }

    internal sealed class Award
    {
        internal Award(string source, float amount)
        {
            Source = source;
            Amount = amount;
        }

        internal string Source { get; }
        internal float Amount { get; }
    }
}
