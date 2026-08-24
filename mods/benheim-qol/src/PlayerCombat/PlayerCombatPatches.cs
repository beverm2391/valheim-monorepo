using HarmonyLib;

namespace BenheimQoL.PlayerCombat;

internal static class AcceptedPlayerHealthLossObservation
{
    internal static PlayerCombatContext? Capture(Character character)
    {
        return character is Player player && player == Player.m_localPlayer
            ? PlayerCombatContext.Capture(player)
            : null;
    }

    internal static void Complete(
        Character character,
        PlayerCombatContext? before,
        AcceptedHealthLossSource source)
    {
        if (before == null || character != before.Player)
        {
            return;
        }

        PlayerCombatContext after = PlayerCombatContext.Capture(before.Player);
        if (after.Health < before.Health)
        {
            PlayerCombatRuntime.Publish(new AcceptedPlayerDamage(before, after, source));
        }
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
internal static class AcceptedPlayerDamagePatch
{
    private static void Prefix(Character __instance, out PlayerCombatContext? __state)
    {
        __state = AcceptedPlayerHealthLossObservation.Capture(__instance);
    }

    private static void Postfix(Character __instance, PlayerCombatContext? __state)
    {
        AcceptedPlayerHealthLossObservation.Complete(
            __instance,
            __state,
            AcceptedHealthLossSource.Damage);
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.UseHealth))]
internal static class AcceptedPlayerHealthCostPatch
{
    private static void Prefix(Character __instance, out PlayerCombatContext? __state)
    {
        __state = AcceptedPlayerHealthLossObservation.Capture(__instance);
    }

    private static void Postfix(Character __instance, PlayerCombatContext? __state)
    {
        AcceptedPlayerHealthLossObservation.Complete(
            __instance,
            __state,
            AcceptedHealthLossSource.HealthCost);
    }
}

[HarmonyPatch(typeof(Player), "OnDeath")]
internal static class PlayerCombatDeathPatch
{
    private static void Prefix(Player __instance)
    {
        PerfectDefenseObservation.Reset();
        PlayerCombatRuntime.Publish(
            new PlayerCombatEnded(__instance, PlayerCombatEndReason.Death));
    }
}

[HarmonyPatch(typeof(Player), "OnDestroy")]
internal static class PlayerCombatPlayerDestroyedPatch
{
    private static void Prefix(Player __instance)
    {
        PerfectDefenseObservation.Reset();
        PlayerCombatRuntime.Publish(
            new PlayerCombatEnded(__instance, PlayerCombatEndReason.PlayerDestroyed));
    }
}

[HarmonyPatch(typeof(ZNet), "OnDestroy")]
internal static class PlayerCombatWorldDestroyedPatch
{
    private static void Prefix()
    {
        PerfectDefenseObservation.Reset();
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
