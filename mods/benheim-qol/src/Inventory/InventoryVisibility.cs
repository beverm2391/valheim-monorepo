using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class InventoryVisibility
{
    private static readonly FieldInfo AnimatorField =
        AccessTools.Field(typeof(InventoryGui), "m_animator");

    internal static bool IsOpen(InventoryGui inventoryGui)
    {
        Animator? animator = (Animator?)AnimatorField.GetValue(inventoryGui);
        return animator != null && animator.GetBool("visible");
    }
}
