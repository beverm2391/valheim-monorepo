using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BenheimQoL.Patches;

internal static class PocketAndQuickStackPatch
{
    private static readonly FieldInfo CurrentContainerField =
        AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

    private static readonly FieldInfo PlayerGridField =
        AccessTools.Field(typeof(InventoryGui), "m_playerGrid");

    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    private static class TogglePocketPatch
    {
        private static bool Prefix(InventoryGrid grid, ItemDrop.ItemData item)
        {
            if (!InputState.IsAltHeld() || item == null || Player.m_localPlayer == null)
            {
                return true;
            }

            Inventory playerInventory = Player.m_localPlayer.GetInventory();
            if (grid.GetInventory() != playerInventory)
            {
                return true;
            }

            if (!PocketItems.Toggle(item, out bool pocketed))
            {
                return true;
            }

            string verb = pocketed ? "Pocketed" : "Unpocketed";
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{verb} {PocketItems.GetDisplayName(item)}");
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "Update")]
    private static class QuickStackHotkeyPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (TextInput.IsVisible()
                || Console.IsVisible()
                || Player.m_localPlayer == null)
            {
                return;
            }

            try
            {
                if (Input.GetKeyDown(KeyCode.P))
                {
                    InventoryGrid playerGrid = (InventoryGrid)PlayerGridField.GetValue(__instance);
                    ItemDrop.ItemData hoveredItem = playerGrid.GetItem(new Vector2i((int)ZInput.mousePosition.x, (int)ZInput.mousePosition.y));
                    ToggleHoveredItem(playerGrid, hoveredItem);
                }

                if (InputState.IsAltHeld() && Input.GetKeyDown(KeyCode.Q))
                {
                    Container? currentContainer = (Container?)CurrentContainerField.GetValue(__instance);
                    QuickStack.Run(Player.m_localPlayer, __instance, currentContainer);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Pocket/quick stack hotkey failed: {ex.Message}");
            }
        }

        private static void ToggleHoveredItem(InventoryGrid playerGrid, ItemDrop.ItemData item)
        {
            if (item == null || playerGrid.GetInventory() != Player.m_localPlayer.GetInventory())
            {
                return;
            }

            if (!PocketItems.Toggle(item, out bool pocketed))
            {
                return;
            }

            string verb = pocketed ? "Pocketed" : "Unpocketed";
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{verb} {PocketItems.GetDisplayName(item)}");
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), "UpdateInventory")]
    private static class PocketMarkerPatch
    {
        private static readonly FieldInfo ElementsField =
            AccessTools.Field(typeof(InventoryGrid), "m_elements");

        private static readonly FieldInfo ElementPositionField =
            AccessTools.Field(AccessTools.Inner(typeof(InventoryGrid), "Element"), "m_pos");

        private static readonly FieldInfo ElementGameObjectField =
            AccessTools.Field(AccessTools.Inner(typeof(InventoryGrid), "Element"), "m_go");

        private static void Postfix(InventoryGrid __instance, Inventory inventory)
        {
            if (Player.m_localPlayer == null || inventory != Player.m_localPlayer.GetInventory())
            {
                return;
            }

            IEnumerable elements = (IEnumerable)ElementsField.GetValue(__instance);
            foreach (object element in elements)
            {
                Vector2i position = (Vector2i)ElementPositionField.GetValue(element);
                GameObject go = (GameObject)ElementGameObjectField.GetValue(element);
                TMP_Text marker = GetOrCreateMarker(go);
                ItemDrop.ItemData item = inventory.GetItemAt(position.x, position.y);
                marker.gameObject.SetActive(item != null && PocketItems.IsManuallyPocketed(item));
            }
        }

        private static TMP_Text GetOrCreateMarker(GameObject inventoryElement)
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
            text.color = new Color(1f, 0.86f, 0.25f, 1f);
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
}
