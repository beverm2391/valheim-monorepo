using System;
using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.Adrenaline;

[HarmonyPatch(typeof(Humanoid), "BlockAttack")]
internal static class PerfectParryContextPatch
{
    private static void Prefix(Humanoid __instance, Character attacker)
    {
        AdrenalineFeedback.BeginPerfectParry(__instance, attacker);
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
    private static void Prefix(Player __instance)
    {
        AdrenalineFeedback.BeginPerfectDodge(__instance);
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
    private static void Prefix(Player __instance, ref float v, out AdrenalineFeedback.Award? __state)
    {
        if (v > 0f)
        {
            float requested = v;
            v *= 2f;
            Diagnostics.Event(
                "Adrenaline",
                "positive_grant_doubled",
                $"requested={requested:0.###} doubled={v:0.###} local={Diagnostics.Bool(__instance == Player.m_localPlayer)}");
        }

        __state = AdrenalineFeedback.CaptureAward(__instance, v);
        AdrenalineFeedback.BeginModifiedAmountCapture(__state, __instance.GetSEMan());
    }

    private static void Postfix(Player __instance, AdrenalineFeedback.Award? __state)
    {
        AdrenalineFeedback.ShowAward(__instance, __state);
        AdrenalineFeedback.EndModifiedAmountCapture(__state);
    }

    private static Exception? Finalizer(Exception? __exception, AdrenalineFeedback.Award? __state)
    {
        AdrenalineFeedback.EndModifiedAmountCapture(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyAdrenaline))]
internal static class AdrenalineModifiedAmountPatch
{
    private static void Postfix(SEMan __instance, ref float use)
    {
        AdrenalineFeedback.CaptureModifiedAmount(__instance, use);
    }
}

[HarmonyPatch(typeof(Hud), "UpdateAdrenaline")]
internal static class AdrenalineHudPatch
{
    private static void Postfix(Hud __instance, Player player)
    {
        AdrenalineHud.Update(__instance, player);
    }
}
