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
    /// Reads only literal number-key presses while the local player has the
    /// Cultivator picker open. The frame marker lets the separate native
    /// UseHotbarItem hook suppress only the matching keyboard action; controller
    /// and rebound hotbar actions remain native.
    /// </summary>
    internal static void UpdateGridSelection(Player player)
    {
        if (!IsCultivatorPieceSelectionOpen(player)
            || !TryGetPressedNumber(out int number))
        {
            return;
        }

        suppressedHotbarFrame = Time.frameCount;
        suppressedHotbarIndex = number <= 8 ? number : 0;

        if (!FarmingGridSelection.TrySelect(number))
        {
            player.Message(MessageHud.MessageType.TopLeft, "Planting grid: use 1, 3, 5, 7, or 9");
            return;
        }

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

    private static bool TryGetPressedNumber(out int number)
    {
        for (int candidate = FarmingSettings.MinimumGridSize;
             candidate <= FarmingSettings.MaximumGridSize;
             candidate++)
        {
            KeyCode alpha = (KeyCode)((int)KeyCode.Alpha0 + candidate);
            KeyCode keypad = (KeyCode)((int)KeyCode.Keypad0 + candidate);
            if (Input.GetKeyDown(alpha) || Input.GetKeyDown(keypad))
            {
                number = candidate;
                return true;
            }
        }

        number = 0;
        return false;
    }
}
