using System.Collections;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class PocketMarker
{
    internal static readonly Color ManualColor = new Color(1f, 0.86f, 0.25f, 1f);
    internal static readonly Color AutomaticColor = new Color(0.35f, 0.84f, 1f, 1f);

    private static readonly FieldInfo ElementsField =
        AccessTools.Field(typeof(InventoryGrid), "m_elements");

    private static readonly FieldInfo ElementPositionField =
        AccessTools.Field(AccessTools.Inner(typeof(InventoryGrid), "Element"), "m_pos");

    private static readonly FieldInfo ElementGameObjectField =
        AccessTools.Field(AccessTools.Inner(typeof(InventoryGrid), "Element"), "m_go");

    internal static void Refresh(InventoryGrid inventoryGrid, Inventory inventory)
    {
        Player player = Player.m_localPlayer;
        if (!player || inventory != player.GetInventory())
        {
            return;
        }

        IEnumerable elements = (IEnumerable)ElementsField.GetValue(inventoryGrid);
        foreach (object element in elements)
        {
            Vector2i position = (Vector2i)ElementPositionField.GetValue(element);
            GameObject go = (GameObject)ElementGameObjectField.GetValue(element);
            TMP_Text marker = GetOrCreate(go);
            ItemDrop.ItemData item = inventory.GetItemAt(position.x, position.y);
            bool manuallyProtected = item != null && PocketItems.IsManuallyPocketed(item);
            bool automaticallyProtected = item != null && PocketItems.IsAutomaticallyProtected(player, item);
            marker.gameObject.SetActive(manuallyProtected || automaticallyProtected);
            if (manuallyProtected || automaticallyProtected)
            {
                marker.color = manuallyProtected ? ManualColor : AutomaticColor;
            }
        }
    }

    private static TMP_Text GetOrCreate(GameObject inventoryElement)
    {
        Transform existing = inventoryElement.transform.Find("benheim-pocket-marker");
        if (existing)
        {
            return existing.GetComponent<TMP_Text>();
        }

        GameObject markerObject = new GameObject("benheim-pocket-marker");
        markerObject.transform.SetParent(inventoryElement.transform, worldPositionStays: false);

        RectTransform rect = markerObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(3f, -2f);
        rect.sizeDelta = new Vector2(20f, 18f);

        TMP_Text text = markerObject.AddComponent<TextMeshProUGUI>();
        TMP_Text? template = GetTemplateText(inventoryElement);
        if (template != null)
        {
            text.font = template.font;
            text.fontMaterial = template.fontMaterial;
        }

        text.text = "P";
        text.fontSize = 14f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static TMP_Text? GetTemplateText(GameObject inventoryElement)
    {
        Transform binding = inventoryElement.transform.Find("binding");
        if (binding)
        {
            return binding.GetComponent<TMP_Text>();
        }

        Transform amount = inventoryElement.transform.Find("amount");
        return amount ? amount.GetComponent<TMP_Text>() : null;
    }
}
