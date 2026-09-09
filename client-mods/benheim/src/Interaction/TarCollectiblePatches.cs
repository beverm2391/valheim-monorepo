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
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo nativeCheck = AccessTools.Field(
                typeof(Pickable),
                nameof(Pickable.m_tarPreventsPicking))
            ?? throw new InvalidOperationException(
                "Benheim could not find Valheim's Pickable tar pickup check.");
        MethodInfo replacement = AccessTools.Method(
                typeof(TarCollectibleInteraction),
                nameof(TarCollectibleInteraction.ShouldBlockPickable))
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
internal static class TarItemDropInteractionPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return TarItemDropCheckTranspiler.ReplaceSingleCheck(
            instructions,
            "ItemDrop.Interact");
    }
}

// Valheim declares AutoPickup private, so nameof(Player.AutoPickup) cannot
// reference it. The patch target must use the method name string.
[HarmonyPatch(typeof(Player), "AutoPickup", new[] { typeof(float) })]
internal static class TarItemDropAutoPickupPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return TarItemDropCheckTranspiler.ReplaceSingleCheck(
            instructions,
            "Player.AutoPickup");
    }
}

internal static class TarItemDropCheckTranspiler
{
    internal static IEnumerable<CodeInstruction> ReplaceSingleCheck(
        IEnumerable<CodeInstruction> instructions,
        string nativeMethod)
    {
        MethodInfo nativeCheck = AccessTools.Method(typeof(ItemDrop), nameof(ItemDrop.InTar))
            ?? throw new InvalidOperationException(
                "Benheim could not find Valheim's ItemDrop tar pickup check.");
        MethodInfo replacement = AccessTools.Method(
                typeof(TarCollectibleInteraction),
                nameof(TarCollectibleInteraction.ShouldBlockItemDrop))
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
            // pickup logic stays intact, as do other ItemDrop.InTar callers
            // such as TimedDestruction.
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
