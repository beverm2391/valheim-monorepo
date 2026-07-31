using System.Reflection;
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
        }
    }

    internal static void BeginPerfectDodge(Player player)
    {
        bool alreadyAwarded = (bool)BeenHitWhileDodgingField.GetValue(player);
        if (player == Player.m_localPlayer && !alreadyAwarded)
        {
            currentSource = "Perfect dodge";
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
