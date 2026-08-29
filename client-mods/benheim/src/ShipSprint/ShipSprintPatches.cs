using System;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.ShipSprint;

internal static class ShipSprintPatches
{
    [HarmonyPatch(typeof(ZNet), "Awake")]
    private static class NetworkAwakePatch
    {
        private static void Postfix() => ShipSprintRuntime.RegisterNetwork();
    }

    [HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.ApplyControlls))]
    private static class ShipControlsPatch
    {
        private static void Postfix(ShipControlls __instance)
        {
            // The run parameter is cleared by PlayerController at steady sail.
            // Read the same native logical controls here so rebinding and gamepad
            // behavior stay Valheim-owned without inventing another keybind.
            ShipSprintRuntime.SampleLocalControl(__instance, InputState.IsNativeRunHeld());
        }
    }

    [HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.OnUseStop))]
    private static class ShipControlStopPatch
    {
        private static void Prefix(ShipControlls __instance) =>
            ShipSprintRuntime.StopLocalControl(__instance);
    }

    [HarmonyPatch(typeof(Ship), nameof(Ship.CustomFixedUpdate))]
    private static class ShipPhysicsPatch
    {
        private static void Prefix(Ship __instance, out ShipSprintPhysicsScope __state) =>
            __state = ShipSprintRuntime.BeginPhysics(__instance);

        private static Exception? Finalizer(
            Ship __instance,
            ShipSprintPhysicsScope __state,
            Exception? __exception)
        {
            ShipSprintRuntime.EndPhysics(__instance, __state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Hud), "LateUpdate")]
    private static class ShipHudPatch
    {
        private static void Postfix(Hud __instance) => ShipSprintHud.Update(__instance);
    }

    [HarmonyPatch(typeof(Hud), "OnDestroy")]
    private static class ShipHudDestroyPatch
    {
        private static void Prefix(Hud __instance) => ShipSprintHud.Destroy(__instance);
    }

    [HarmonyPatch(typeof(Ship), "GetSailForce")]
    private static class ShipSailForcePatch
    {
        private static void Postfix(Ship __instance, float sailSize, ref Vector3 __result) =>
            ShipSprintRuntime.MultiplySailForce(__instance, sailSize, ref __result);
    }

    [HarmonyPatch(typeof(Ship), "OnDisable")]
    private static class ShipDisablePatch
    {
        private static void Prefix(Ship __instance) =>
            ShipSprintRuntime.Teardown(__instance, "ship_disabled");
    }

    [HarmonyPatch(typeof(Ship), "OnDestroyed")]
    private static class ShipDestroyedPatch
    {
        private static void Prefix(Ship __instance) =>
            ShipSprintRuntime.Teardown(__instance, "ship_destroyed");
    }

    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    private static class WorldDestroyPatch
    {
        private static void Prefix() => ShipSprintRuntime.Reset("world_lost");
    }
}
