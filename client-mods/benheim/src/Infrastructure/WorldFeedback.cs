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
    private static readonly FieldInfo? GuiField = AccessTools.Field(
        AccessTools.Inner(typeof(DamageText), "WorldTextInstance"),
        "m_gui");
    private static readonly FieldInfo? TextField = AccessTools.Field(
        AccessTools.Inner(typeof(DamageText), "WorldTextInstance"),
        "m_textField");

    internal static void ShowAbovePlayer(Player player, string text)
    {
        ShowAt(player.transform.position + Vector3.up * 1.9f, text);
    }

    internal static void ShowAbove(Transform anchor, Vector3 offset, string text)
    {
        ShowAt(anchor.position + offset, text);
    }

    /// <summary>
    /// Creates the same local Bonus overlay used by Perfect Parry, then removes
    /// it from DamageText's transient update list so its caller can keep it
    /// stationary instead of inheriting the native rise, fade, and lifetime.
    /// </summary>
    internal static bool TryCreatePersistentBonusText(
        Vector3 worldPosition,
        out GameObject root,
        out TMP_Text text)
    {
        root = null!;
        text = null!;
        DamageText damageText = DamageText.instance;
        IList? worldTexts = damageText && WorldTextsField != null
            ? WorldTextsField.GetValue(damageText) as IList
            : null;
        if (!damageText || worldTexts == null || AddInworldTextMethod == null)
        {
            return false;
        }

        object? instance = AddBonusText(
            damageText,
            worldTexts,
            worldPosition,
            0f,
            string.Empty);
        if (instance == null)
        {
            return false;
        }

        GameObject? createdRoot = GuiField?.GetValue(instance) as GameObject;
        TMP_Text? createdText = TextField?.GetValue(instance) as TMP_Text;
        worldTexts.Remove(instance);
        if (!createdRoot || !createdText)
        {
            if (createdRoot)
            {
                Object.Destroy(createdRoot);
            }

            return false;
        }

        createdRoot.name = "Benheim Persistent Bonus Text";
        createdRoot.hideFlags = HideFlags.DontSave;
        createdText.richText = false;
        createdText.raycastTarget = false;
        createdRoot.SetActive(false);
        root = createdRoot;
        text = createdText;
        return true;
    }

    internal static bool PlacePersistentText(
        GameObject root,
        Vector3 worldPosition,
        Camera camera)
    {
        Vector3 screenPosition = camera.WorldToScreenPointScaled(worldPosition);
        root.transform.position = screenPosition;
        return screenPosition.x >= 0f &&
            screenPosition.x <= Screen.width &&
            screenPosition.y >= 0f &&
            screenPosition.y <= Screen.height &&
            screenPosition.z >= 0f;
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
