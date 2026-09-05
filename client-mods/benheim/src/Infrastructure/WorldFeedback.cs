using System.Collections;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BenheimQoL.Infrastructure;

internal static class WorldFeedback
{
    private const float UtilityTextDurationSeconds = 3f;

    private static readonly MethodInfo? AddInworldTextMethod = AccessTools.DeclaredMethod(
        typeof(DamageText),
        "AddInworldText",
        new[] { typeof(DamageText.TextType), typeof(Vector3), typeof(float), typeof(string), typeof(bool) });

    private static readonly FieldInfo? WorldTextsField = AccessTools.Field(typeof(DamageText), "m_worldTexts");
    private static readonly FieldInfo? DurationField = AccessTools.Field(
        AccessTools.Inner(typeof(DamageText), "WorldTextInstance"),
        "m_duration");
    internal static void ShowAbovePlayer(Player player, string text)
    {
        ShowAt(player.transform.position + Vector3.up * 1.9f, text);
    }

    internal static void ShowAbove(Transform anchor, Vector3 offset, string text)
    {
        ShowAt(anchor.position + offset, text);
    }

    private static void ShowAt(Vector3 position, string text)
    {
        DamageText damageText = DamageText.instance;
        Camera camera = Utils.GetMainCamera();
        if (!damageText || !camera || Hud.IsUserHidden() || AddInworldTextMethod == null)
        {
            return;
        }

        float distance = Vector3.Distance(camera.transform.position, position);
        IList? worldTexts = WorldTextsField?.GetValue(damageText) as IList;
        object? instance = AddBonusText(damageText, worldTexts, position, distance, text);
        if (instance != null)
        {
            DurationField?.SetValue(instance, UtilityTextDurationSeconds);
        }
    }

    private static object? AddBonusText(
        DamageText damageText,
        IList? worldTexts,
        Vector3 position,
        float distance,
        string text)
    {
        int previousCount = worldTexts?.Count ?? 0;
        AddInworldTextMethod!.Invoke(
            damageText,
            new object[] { DamageText.TextType.Bonus, position, distance, text, false });
        return worldTexts != null && worldTexts.Count > previousCount
            ? worldTexts[worldTexts.Count - 1]
            : null;
    }
}
