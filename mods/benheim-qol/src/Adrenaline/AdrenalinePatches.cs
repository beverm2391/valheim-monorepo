using System;
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
    private static void Prefix(Player __instance, float v, out AdrenalineFeedback.Award? __state)
    {
        __state = AdrenalineFeedback.CaptureAward(__instance, v);
    }

    private static void Postfix(Player __instance, AdrenalineFeedback.Award? __state)
    {
        AdrenalineFeedback.ShowAward(__instance, __state);
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
