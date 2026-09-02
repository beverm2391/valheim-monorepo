using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Farming;

internal static class FarmingInput
{
    private static int suppressedHotbarFrame = -1;
    private static int suppressedHotbarIndex;

    internal static bool IsMassActionHeld()
    {
        if (InputState.IsTextEntryActive())
        {
            return false;
        }

        return InputState.IsShiftHeld()
            || Input.GetKey(KeyCode.JoystickButton4)
            || ZInput.GetKey(KeyCode.JoystickButton4);
    }

    /// <summary>
    /// Handles only Left Shift plus an odd-number key, with no other modifier
    /// held, while the local player has the Cultivator picker open. It records
    /// the current frame so the separate native UseHotbarItem hook suppresses
    /// the same keyboard action. Every other number-key path remains native.
    /// </summary>
    internal static void UpdateGridSelection(Player player)
    {
        if (Player.m_localPlayer != player)
        {
            return;
        }

        bool pickerOpen = IsCultivatorPieceSelectionOpen(player);
        if (FarmingGridSelection.UpdatePickerSession(pickerOpen))
        {
            PlantingPreview.DestroyGhosts();
        }

        bool leftShiftHeld = IsLeftShiftHeld();
        bool anotherModifierHeld = IsAnotherModifierHeld();
        if (!pickerOpen
            || !leftShiftHeld
            || anotherModifierHeld
            || !TryGetPressedGridSize(out int number)
            || !FarmingGridSelection.ShouldHandleInput(
                pickerOpen,
                leftShiftHeld,
                anotherModifierHeld,
                number))
        {
            return;
        }

        suppressedHotbarFrame = Time.frameCount;
        suppressedHotbarIndex = number <= 8 ? number : 0;

        FarmingGridSelection.TrySelect(number);

        PlantingPreview.DestroyGhosts();
        player.Message(MessageHud.MessageType.TopLeft, $"Planting grid: {number}x{number}");
        Diagnostics.Event("Farming", "plant_grid_selected", $"grid={number}x{number}");
    }

    internal static bool ShouldSuppressHotbarUse(Player player, int index)
    {
        return Time.frameCount == suppressedHotbarFrame
            && index == suppressedHotbarIndex
            && IsCultivatorPieceSelectionOpen(player);
    }

    internal static void ResetGridSelection()
    {
        suppressedHotbarFrame = -1;
        suppressedHotbarIndex = 0;
        FarmingGridSelection.Reset();
    }

    private static bool IsCultivatorPieceSelectionOpen(Player player)
    {
        if (Player.m_localPlayer != player
            || InputState.IsTextEntryActive()
            || !Hud.IsPieceSelectionVisible())
        {
            return false;
        }

        ItemDrop.ItemData? rightItem = FarmingReflection.GetRightItem(player);
        PieceTable? activePieces = FarmingReflection.BuildPiecesField.GetValue(player) as PieceTable;
        return rightItem != null
            && rightItem.m_dropPrefab
            && rightItem.m_dropPrefab.name == "Cultivator"
            && rightItem.m_shared.m_buildPieces != null
            && object.ReferenceEquals(rightItem.m_shared.m_buildPieces, activePieces);
    }

    private static bool TryGetPressedGridSize(out int number)
    {
        for (int candidate = FarmingSettings.MinimumGridSize;
             candidate <= FarmingSettings.MaximumGridSize;
             candidate += 2)
        {
            KeyCode alpha = (KeyCode)((int)KeyCode.Alpha0 + candidate);
            KeyCode keypad = (KeyCode)((int)KeyCode.Keypad0 + candidate);
            if (InputState.IsKeyDown(alpha) || InputState.IsKeyDown(keypad))
            {
                number = candidate;
                return true;
            }
        }

        number = 0;
        return false;
    }

    private static bool IsLeftShiftHeld()
    {
        if (InputState.IsTextEntryActive())
        {
            return false;
        }

        return Input.GetKey(KeyCode.LeftShift) || ZInput.GetKey(KeyCode.LeftShift);
    }

    private static bool IsAnotherModifierHeld()
    {
        return Input.GetKey(KeyCode.RightShift)
            || Input.GetKey(KeyCode.LeftAlt)
            || Input.GetKey(KeyCode.RightAlt)
            || Input.GetKey(KeyCode.LeftControl)
            || Input.GetKey(KeyCode.RightControl)
            || Input.GetKey(KeyCode.AltGr)
            || Input.GetKey(KeyCode.LeftCommand)
            || Input.GetKey(KeyCode.RightCommand)
            || Input.GetKey(KeyCode.LeftMeta)
            || Input.GetKey(KeyCode.RightMeta)
            || Input.GetKey(KeyCode.LeftWindows)
            || Input.GetKey(KeyCode.RightWindows)
            || ZInput.GetKey(KeyCode.RightShift)
            || ZInput.GetKey(KeyCode.LeftAlt)
            || ZInput.GetKey(KeyCode.RightAlt)
            || ZInput.GetKey(KeyCode.LeftControl)
            || ZInput.GetKey(KeyCode.RightControl)
            || ZInput.GetKey(KeyCode.AltGr)
            || ZInput.GetKey(KeyCode.LeftCommand)
            || ZInput.GetKey(KeyCode.RightCommand)
            || ZInput.GetKey(KeyCode.LeftMeta)
            || ZInput.GetKey(KeyCode.RightMeta)
            || ZInput.GetKey(KeyCode.LeftWindows)
            || ZInput.GetKey(KeyCode.RightWindows);
    }
}
