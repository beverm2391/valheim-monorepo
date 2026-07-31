using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Patches;

internal static class AdrenalineFeedback
{
    private static string? currentSource;

    internal static string? CurrentSource => currentSource;

    internal static void Begin(string source)
    {
        currentSource = source;
    }

    internal static void End()
    {
        currentSource = null;
    }
}

[HarmonyPatch(typeof(Humanoid), "BlockAttack")]
internal static class PerfectParryContextPatch
{
    private static readonly FieldInfo BlockTimerField =
        AccessTools.Field(typeof(Humanoid), "m_blockTimer");
    private static readonly FieldInfo LeftItemField =
        AccessTools.Field(typeof(Humanoid), "m_leftItem");

    private static void Prefix(Humanoid __instance, Character attacker)
    {
        if (__instance != Player.m_localPlayer || !attacker)
        {
            return;
        }

        float blockTimer = (float)BlockTimerField.GetValue(__instance);
        ItemDrop.ItemData? blocker = (ItemDrop.ItemData?)LeftItemField.GetValue(__instance)
            ?? __instance.GetCurrentWeapon();
        if (blocker?.m_shared.m_timedBlockBonus > 1f &&
            blockTimer >= 0f &&
            blockTimer < 0.25f)
        {
            AdrenalineFeedback.Begin("PARRY");
        }
    }

    private static Exception? Finalizer(Exception? __exception)
    {
        AdrenalineFeedback.End();
        return __exception;
    }
}

[HarmonyPatch(typeof(Player), "RPC_HitWhileDodging")]
internal static class PerfectDodgeContextPatch
{
    private static readonly FieldInfo BeenHitWhileDodgingField =
        AccessTools.Field(typeof(Player), "m_beenHitWhileDodging");

    private static void Prefix(Player __instance)
    {
        bool alreadyAwarded = (bool)BeenHitWhileDodgingField.GetValue(__instance);
        if (__instance == Player.m_localPlayer && !alreadyAwarded)
        {
            AdrenalineFeedback.Begin("PERFECT DODGE");
        }
    }

    private static Exception? Finalizer(Exception? __exception)
    {
        AdrenalineFeedback.End();
        return __exception;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.AddAdrenaline))]
internal static class AdrenalineAwardFeedbackPatch
{
    private sealed class FeedbackState
    {
        internal FeedbackState(string source, float award, float before, float maximum)
        {
            Source = source;
            Award = award;
            Before = before;
            Maximum = maximum;
        }

        internal string Source { get; }
        internal float Award { get; }
        internal float Before { get; }
        internal float Maximum { get; }
    }

    private static void Prefix(Player __instance, float v, out FeedbackState? __state)
    {
        __state = null;
        string? source = AdrenalineFeedback.CurrentSource;
        if (__instance != Player.m_localPlayer || source == null || v <= 0f)
        {
            return;
        }

        float maximum = __instance.GetMaxAdrenaline();
        if (maximum <= 0f)
        {
            return;
        }

        float before = __instance.GetAdrenaline();
        float fill = before / maximum;
        float award = v * Game.m_adrenalineRate;
        award *= __instance.m_adrenalineGainMultiplier.Evaluate(fill);
        __instance.GetSEMan().ModifyAdrenaline(award, ref award);
        __state = new FeedbackState(source, award, before, maximum);
    }

    private static void Postfix(Player __instance, FeedbackState? __state)
    {
        if (__state == null || __state.Award <= 0f)
        {
            return;
        }

        float after = __instance.GetAdrenaline();
        bool activated = __state.Before + __state.Award >= __state.Maximum &&
            after < __state.Maximum;
        string result = activated
            ? "ACTIVATED"
            : $"{after:0.#}/{__state.Maximum:0.#}";
        string text = $"{__state.Source} +{__state.Award:0.#} | {result}";
        DamageText.instance?.ShowText(
            DamageText.TextType.Bonus,
            __instance.transform.position + Vector3.up * 1.75f,
            text,
            player: true);
    }
}
