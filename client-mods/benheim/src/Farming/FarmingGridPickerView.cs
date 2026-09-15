using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Farming;

// Copy only presentation from the loaded native donor. Cloning TakeAll or a
// build piece would also copy native handlers that transfer items or close Hud.
internal sealed class FarmingGridPickerView
{
    private static readonly FieldInfo? PieceViewField =
        AccessTools.Field(typeof(BuildUi), "m_pieceView");

    private readonly GameObject root;
    private readonly Button[] buttons;
    private readonly TMP_Text[] labels;
    private readonly ColorBlock nativeColors;
    private readonly Color nativeTextColor;

    private FarmingGridPickerView(GameObject root, Button[] buttons, TMP_Text[] labels,
        ColorBlock nativeColors, Color nativeTextColor)
    {
        this.root = root;
        this.buttons = buttons;
        this.labels = labels;
        this.nativeColors = nativeColors;
        this.nativeTextColor = nativeTextColor;
    }

    internal bool IsAlive => root != null && root.activeInHierarchy;
    internal int HighlightedSize { get; private set; }

    internal static FarmingGridPickerView? TryCreate(Hud hud, Action<int> select, out string failure)
    {
        failure = string.Empty;
        BuildUi? buildUi = hud.m_buildUi;
        RectTransform? buildRoot = buildUi?.transform as RectTransform;
        RectTransform? pieceView = buildUi == null || PieceViewField == null
            ? null
            : PieceViewField.GetValue(buildUi) as RectTransform;
        Button? nativeButton = InventoryGui.instance?.m_takeAllButton;
        Transform? donor = nativeButton?.transform;
        Image? buttonImage = nativeButton?.targetGraphic as Image ?? donor?.GetComponent<Image>();
        TMP_Text? text = donor?.GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (buildRoot == null) failure = "build_ui_missing";
        else if (pieceView == null) failure = "piece_view_missing";
        else if (nativeButton == null || buttonImage == null) failure = "native_button_missing";
        else if (text == null || text.font == null || text.fontSharedMaterial == null) failure = "native_button_text_missing";
        if (failure.Length > 0) return null;

        RectTransform? row = null;
        try
        {
            row = CreateRect("BenheimPlantingGrid", buildRoot!);
            // Valheim 1.0 moved the live picker from Hud.m_pieceSelectionWindow
            // into BuildUi. Place the row above the live piece viewport so it
            // follows that UI at every resolution without entering its scroll
            // mask or depending on the retired picker hierarchy.
            Vector3 pieceTop = buildRoot!.InverseTransformPoint(pieceView!.TransformPoint(
                new Vector3(pieceView.rect.center.x, pieceView.rect.yMax, 0f)));
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0f);
            row.sizeDelta = new Vector2(356f, 44f);
            row.localPosition = new Vector3(pieceTop.x, pieceTop.y + 6f, 0f);
            row.SetAsLastSibling();

            Button[] buttons = new Button[5];
            TMP_Text[] labels = new TMP_Text[buttons.Length];
            for (int index = 0; index < buttons.Length; index++)
            {
                int size = 1 + index * 2;
                RectTransform rect = CreateRect("Grid" + size, row);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(6f + index * 70f, 0f);
                rect.sizeDelta = new Vector2(64f, 32f);
                Image image = CopyImage(buttonImage!, rect.gameObject.AddComponent<Image>());
                Button button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.ColorTint;
                button.colors = nativeButton!.colors;
                // These are mouse choices within Hud. Do not steal native
                // gamepad piece navigation or submit a cell on keyboard input.
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => select(size));
                buttons[index] = button;

                RectTransform labelRect = CreateRect("Label", rect);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(4f, 2f);
                labelRect.offsetMax = new Vector2(-4f, -2f);
                TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
                label.font = text!.font;
                label.fontSharedMaterial = text.fontSharedMaterial;
                label.color = text.color;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 20f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                label.text = $"{size}x{size}";
                labels[index] = label;
            }
            return new FarmingGridPickerView(row.gameObject, buttons, labels,
                nativeButton!.colors, text!.color);
        }
        catch
        {
            if (row != null)
            {
                row.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(row.gameObject);
            }
            throw;
        }
    }

    internal void Highlight(int size)
    {
        for (int index = 0; index < buttons.Length; index++)
        {
            bool selected = size == 1 + index * 2;
            ColorBlock colors = nativeColors;
            if (selected)
            {
                colors.normalColor = colors.highlightedColor = colors.selectedColor = new Color(1f, 0.75f, 0.35f, 1f);
            }
            buttons[index].colors = colors;
            labels[index].color = selected ? Color.white : nativeTextColor;
        }
        HighlightedSize = size;
    }

    internal void Destroy()
    {
        if (root == null) return;
        root.SetActive(false);
        foreach (Button button in buttons)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }
        UnityEngine.Object.Destroy(root);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)result.transform;
        rect.SetParent(parent, worldPositionStays: false);
        return rect;
    }

    private static Image CopyImage(Image source, Image destination)
    {
        destination.sprite = source.sprite;
        destination.overrideSprite = source.overrideSprite;
        destination.type = source.type;
        destination.material = source.material;
        destination.color = source.color;
        destination.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        return destination;
    }
}
