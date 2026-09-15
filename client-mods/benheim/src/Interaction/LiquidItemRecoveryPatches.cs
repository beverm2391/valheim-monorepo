using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.Interaction;

[HarmonyPatch(typeof(ItemDrop), "Awake")]
internal static class ItemDropBuoyancyPatch
{
    private static readonly HashSet<string> ReportedPrefabs =
        new HashSet<string>(StringComparer.Ordinal);

    [HarmonyPrefix]
    private static void Prefix(ItemDrop __instance)
    {
        string prefab = Utils.GetPrefabName(__instance.gameObject);
        try
        {
            ItemBuoyancySetup setup = LiquidItemRecovery.EnsureNativeBuoyancy(__instance);
            if (!ReportedPrefabs.Add(prefab))
            {
                return;
            }

            Floating? floating = __instance.GetComponent<Floating>();
            Diagnostics.Emit(
                DiagnosticEvent.Create("Interaction", "item_buoyancy_setup")
                    .String("prefab", prefab)
                    .String("result", setup.ToString().ToLowerInvariant())
                    .String("liquids", "water_and_tar")
                    .String("mechanism", "native_floating_component")
                    .Boolean("has_rigidbody", __instance.GetComponent<UnityEngine.Rigidbody>() != null)
                    .Boolean("has_collider", __instance.GetComponentInChildren<UnityEngine.Collider>() != null)
                    .Number("water_level_offset", floating?.m_waterLevelOffset ?? 0f)
                    .Number("force", floating?.m_force ?? 0f)
                    .Number("force_distance", floating?.m_forceDistance ?? 0f)
                    .Number("damping", floating?.m_damping ?? 0f));
        }
        catch (Exception exception)
        {
            // A malformed item must not break its native Awake. The missing
            // buoyancy stays visible instead of becoming a silent sink.
            Diagnostics.Emit(
                DiagnosticEvent.Create("Interaction", "item_buoyancy_setup_failed")
                    .String("prefab", prefab)
                    .String("error_type", exception.GetType().Name)
                    .String("error", Diagnostics.Flatten(exception.Message)));
            Plugin.Log.LogWarning(
                $"Benheim could not add native buoyancy to {prefab}: {Diagnostics.Flatten(exception.Message)}");
        }
    }
}

[HarmonyPatch(
    typeof(Pickable),
    nameof(Pickable.Interact),
    new[] { typeof(Humanoid), typeof(bool), typeof(bool) })]
internal static class LiquidPickableTarInteractionPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo nativeCheck = AccessTools.Field(
                typeof(Pickable),
                nameof(Pickable.m_tarPreventsPicking))
            ?? throw new InvalidOperationException(
                "Benheim could not find Valheim's Pickable tar pickup check.");
        MethodInfo replacement = AccessTools.Method(
                typeof(LiquidItemRecovery),
                nameof(LiquidItemRecovery.ShouldBlockPickable))
            ?? throw new InvalidOperationException(
                "Benheim could not find the replacement for Valheim's Pickable tar pickup check.");

        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        int replaced = 0;
        foreach (CodeInstruction code in codes)
        {
            if (code.opcode != OpCodes.Ldfld || !Equals(code.operand, nativeCheck))
            {
                continue;
            }

            // The Pickable already on the stack becomes the helper argument.
            // Floating detection, the RPC, drops, and effects stay native.
            code.opcode = OpCodes.Call;
            code.operand = replacement;
            replaced++;
        }

        if (replaced != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one Pickable tar pickup check, but found {replaced}.");
        }

        return codes;
    }
}

[HarmonyPatch(
    typeof(ItemDrop),
    nameof(ItemDrop.Interact),
    new[] { typeof(Humanoid), typeof(bool), typeof(bool) })]
internal static class LiquidItemDropTarInteractionPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return LiquidItemDropTarCheckTranspiler.ReplaceSingleCheck(
            instructions,
            "ItemDrop.Interact");
    }
}

// Valheim declares AutoPickup private, so nameof(Player.AutoPickup) cannot
// reference it. The patch target must use the method name string.
[HarmonyPatch(typeof(Player), "AutoPickup", new[] { typeof(float) })]
internal static class LiquidItemDropTarAutoPickupPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return LiquidItemDropTarCheckTranspiler.ReplaceSingleCheck(
            instructions,
            "Player.AutoPickup");
    }
}

internal static class LiquidItemDropTarCheckTranspiler
{
    internal static IEnumerable<CodeInstruction> ReplaceSingleCheck(
        IEnumerable<CodeInstruction> instructions,
        string nativeMethod)
    {
        MethodInfo nativeCheck = AccessTools.Method(typeof(ItemDrop), nameof(ItemDrop.InTar))
            ?? throw new InvalidOperationException(
                "Benheim could not find Valheim's ItemDrop tar pickup check.");
        MethodInfo replacement = AccessTools.Method(
                typeof(LiquidItemRecovery),
                nameof(LiquidItemRecovery.ShouldBlockItemDrop))
            ?? throw new InvalidOperationException(
                "Benheim could not find the replacement for Valheim's ItemDrop tar pickup check.");

        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        int replaced = 0;
        foreach (CodeInstruction code in codes)
        {
            if ((code.opcode != OpCodes.Call && code.opcode != OpCodes.Callvirt)
                || !Equals(code.operand, nativeCheck))
            {
                continue;
            }

            // The ItemDrop already on the stack becomes the helper argument.
            // Only this method's tar check changes. The rest of its native
            // pickup logic stays intact, as do other ItemDrop.InTar callers.
            code.opcode = OpCodes.Call;
            code.operand = replacement;
            replaced++;
        }

        if (replaced != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one {nativeMethod} tar pickup check, but found {replaced}.");
        }

        return codes;
    }
}
