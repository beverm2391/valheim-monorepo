using System.Collections.Generic;
using HarmonyLib;

namespace BenheimQoL.Grappling;

[HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
internal static class GrapplingShotPatch
{
    private static void Postfix(Projectile __instance, Character owner, ItemDrop.ItemData item) =>
        GrapplingRuntime.Fired(__instance, owner, item);
}

[HarmonyPatch(typeof(Projectile), "SpawnOnHit")]
internal static class GrapplingHitPatch
{
    private static void Prefix(Projectile __instance, out GrapplingRuntime.Traversal? __state) =>
        __state = GrapplingRuntime.EnterHit(__instance);

    // Finalizers also restore nested native spawn scopes after exceptions.
    private static void Finalizer(GrapplingRuntime.Traversal? __state) => GrapplingRuntime.RestoreScope(__state);
}

[HarmonyPatch(typeof(Projectile), "FixedUpdate")]
internal static class GrapplingFlightPatch
{
    private static void Postfix(Projectile __instance, bool ___m_didHit) =>
        GrapplingRuntime.FlightUpdated(__instance, ___m_didHit);
}

[HarmonyPatch(typeof(GrapplingPoint), nameof(GrapplingPoint.Activate))]
internal static class GrapplingAttachPatch
{
    private static void Prefix(GrapplingPoint __instance, Character character) =>
        GrapplingRuntime.Activate(__instance, character);
}

[HarmonyPatch(typeof(GrapplingPoint), nameof(GrapplingPoint.Break))]
internal static class GrapplingReleasePatch
{
    private static void Prefix(GrapplingPoint __instance, ZNetView ___m_nview, Character ___m_character,
        out GrapplingRuntime.BreakScope __state) =>
        __state = GrapplingRuntime.EnterBreak(__instance, ___m_nview, ___m_character);

    private static void Postfix(GrapplingPoint __instance, bool early, GrapplingRuntime.BreakScope __state) =>
        GrapplingRuntime.Released(__instance, early, __state.Accepted);

    private static void Finalizer(GrapplingRuntime.BreakScope __state) => GrapplingRuntime.RestoreScope(__state.Previous);
}

[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetWeaponLoadingTime))]
internal static class GrapplingReloadPatch
{
    private static void Postfix(ItemDrop.ItemData __instance, ref float __result) =>
        __result = GrapplingRuntime.Reload(__instance, __result);
}

[HarmonyPatch(typeof(Player), "QueueReloadAction")]
internal static class GrapplingReloadEvidencePatch
{
    private static void Prefix(List<Player.MinorActionData> ___m_actionQueue, out int __state) =>
        __state = ___m_actionQueue.Count;

    private static void Postfix(Player __instance, List<Player.MinorActionData> ___m_actionQueue, int __state) =>
        GrapplingRuntime.ObserveReload(__instance, __state, ___m_actionQueue);
}
