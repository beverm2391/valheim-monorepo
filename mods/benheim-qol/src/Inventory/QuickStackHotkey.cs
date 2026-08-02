using System;
using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackHotkey
{
    private static readonly FieldInfo CurrentContainerField =
        AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

    internal static void Update()
    {
        if (!InputState.IsShiftHeld()
            || !InputState.IsKeyDown(KeyCode.P)
            || TextInput.IsVisible()
            || Console.IsVisible())
        {
            return;
        }

        Player? player = Player.m_localPlayer;
        InventoryGui? inventoryGui = InventoryGui.instance;
        if (!player || !inventoryGui)
        {
            Diagnostics.Event("Inventory", "quick_stack_rejected", "reason=gameplay_ui_unavailable");
            return;
        }

        try
        {
            Container? currentContainer = (Container?)CurrentContainerField.GetValue(inventoryGui);
            QuickStack.Run(player, inventoryGui, currentContainer);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Quick stack hotkey failed: {ex.Message}");
        }
    }
}
