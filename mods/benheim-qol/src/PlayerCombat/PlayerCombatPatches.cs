using HarmonyLib;

namespace BenheimQoL.PlayerCombat;

[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
internal static class AcceptedPlayerDamagePatch
{
    private static void Prefix(Character __instance, out PlayerCombatContext? __state)
    {
        __state = __instance is Player player && player == Player.m_localPlayer
            ? PlayerCombatContext.Capture(player)
            : null;
    }

    private static void Postfix(Character __instance, PlayerCombatContext? __state)
    {
        if (__state == null || __instance != __state.Player)
        {
            return;
        }

        PlayerCombatContext after = PlayerCombatContext.Capture(__state.Player);
        if (after.Health < __state.Health)
        {
            PlayerCombatRuntime.Publish(new AcceptedPlayerDamage(__state, after));
        }
    }
}

[HarmonyPatch(typeof(Player), "OnDeath")]
internal static class PlayerCombatDeathPatch
{
    private static void Prefix(Player __instance)
    {
        PlayerCombatRuntime.Publish(
            new PlayerCombatEnded(__instance, PlayerCombatEndReason.Death));
    }
}

[HarmonyPatch(typeof(Player), "OnDestroy")]
internal static class PlayerCombatPlayerDestroyedPatch
{
    private static void Prefix(Player __instance)
    {
        PerfectDefenseObservation.End();
        PlayerCombatRuntime.Publish(
            new PlayerCombatEnded(__instance, PlayerCombatEndReason.PlayerDestroyed));
    }
}

[HarmonyPatch(typeof(ZNet), "OnDestroy")]
internal static class PlayerCombatWorldDestroyedPatch
{
    private static void Prefix()
    {
        PerfectDefenseObservation.End();
        PlayerCombatRuntime.EndWorld();
    }
}

[HarmonyPatch(typeof(ObjectDB), "Awake")]
internal static class EarnedStateObjectDatabaseAwakePatch
{
    private static void Postfix(ObjectDB __instance)
    {
        PlayerCombatRuntime.RegisterNativeEffects(__instance);
    }
}

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
internal static class EarnedStateObjectDatabaseCopyPatch
{
    private static void Postfix(ObjectDB __instance)
    {
        PlayerCombatRuntime.RegisterNativeEffects(__instance);
    }
}
