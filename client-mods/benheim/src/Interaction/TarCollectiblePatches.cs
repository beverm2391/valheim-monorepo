using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BenheimQoL.Interaction;

[HarmonyPatch(
    typeof(Pickable),
    nameof(Pickable.Interact),
    new[] { typeof(Humanoid), typeof(bool), typeof(bool) })]
internal static class TarPickableInteractionPatch
{
    private static void Prefix(
        Pickable __instance,
        out TarCollectibleInteractionObservation __state)
    {
        __state = TarCollectibleInteraction.Observe(__instance);
    }

    private static void Postfix(
        bool __result,
        TarCollectibleInteractionObservation __state)
    {
        TarCollectibleInteraction.ReportResult(__state, __result);
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo nativeGate = AccessTools.Field(
                typeof(Pickable),
                nameof(Pickable.m_tarPreventsPicking))
            ?? throw new InvalidOperationException(
                "Valheim's Pickable tar interaction gate was not found.");
        MethodInfo replacement = AccessTools.Method(
                typeof(TarCollectibleInteraction),
                nameof(TarCollectibleInteraction.ShouldBlockPickable))
            ?? throw new InvalidOperationException(
                "Benheim's Pickable tar interaction replacement was not found.");

        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        int replaced = 0;
        foreach (CodeInstruction code in codes)
        {
            if (code.opcode != OpCodes.Ldfld || !Equals(code.operand, nativeGate))
            {
                continue;
            }

            // The Pickable instance already on the stack becomes the helper
            // argument. Every later native Floating, RPC, and effect path stays
            // byte-for-byte in Valheim's method.
            code.opcode = OpCodes.Call;
            code.operand = replacement;
            replaced++;
        }

        if (replaced != 1)
        {
            throw new InvalidOperationException(
                $"Expected one Pickable tar interaction gate, found {replaced}.");
        }

        return codes;
    }
}

[HarmonyPatch(
    typeof(ItemDrop),
    nameof(ItemDrop.Interact),
    new[] { typeof(Humanoid), typeof(bool), typeof(bool) })]
internal static class TarItemDropInteractionPatch
{
    private static void Prefix(
        ItemDrop __instance,
        bool repeat,
        out TarCollectibleInteractionObservation __state)
    {
        __state = repeat
            ? default
            : TarCollectibleInteraction.Observe(__instance);
    }

    private static void Postfix(
        bool __result,
        TarCollectibleInteractionObservation __state)
    {
        TarCollectibleInteraction.ReportResult(__state, __result);
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo nativeGate = AccessTools.Method(typeof(ItemDrop), nameof(ItemDrop.InTar))
            ?? throw new InvalidOperationException(
                "Valheim's ItemDrop tar interaction gate was not found.");
        MethodInfo replacement = AccessTools.Method(
                typeof(TarCollectibleInteraction),
                nameof(TarCollectibleInteraction.ShouldBlockItemDrop))
            ?? throw new InvalidOperationException(
                "Benheim's ItemDrop tar interaction replacement was not found.");

        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        int replaced = 0;
        foreach (CodeInstruction code in codes)
        {
            if ((code.opcode != OpCodes.Call && code.opcode != OpCodes.Callvirt)
                || !Equals(code.operand, nativeGate))
            {
                continue;
            }

            // The ItemDrop instance already on the stack becomes the helper
            // argument. Pickup, ownership, inventory, and ordinary failure all
            // remain in Valheim's original Interact and Pickup methods.
            code.opcode = OpCodes.Call;
            code.operand = replacement;
            replaced++;
        }

        if (replaced != 1)
        {
            throw new InvalidOperationException(
                $"Expected one ItemDrop tar interaction gate, found {replaced}.");
        }

        return codes;
    }
}
