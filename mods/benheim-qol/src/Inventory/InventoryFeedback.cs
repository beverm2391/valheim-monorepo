using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class InventoryFeedback
{
    private static readonly MethodInfo? AddInworldTextMethod = AccessTools.DeclaredMethod(
        typeof(DamageText),
        "AddInworldText",
        new[] { typeof(DamageText.TextType), typeof(Vector3), typeof(float), typeof(string), typeof(bool) });

    internal static void ShowAbovePlayer(Player player, string text)
    {
        DamageText damageText = DamageText.instance;
        Camera camera = Utils.GetMainCamera();
        if (!damageText || !camera || Hud.IsUserHidden() || AddInworldTextMethod == null)
        {
            return;
        }

        Vector3 position = player.transform.position + Vector3.up * 1.9f;
        float distance = Vector3.Distance(camera.transform.position, position);
        AddInworldTextMethod.Invoke(
            damageText,
            new object[] { DamageText.TextType.Bonus, position, distance, text, false });
    }
}
