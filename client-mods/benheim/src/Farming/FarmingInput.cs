using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Farming;

internal static class FarmingInput
{
    private static int suppressedHotbarIndex;
    private static bool localPlayerUpdateActive;

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
    /// Runs immediately after Valheim updates ZInput. This keeps selection on
    /// the same latched edge that Player.Update uses for native hotbar input;
    /// sampling the low-level InputSystem edge from Player.Update is order-
    /// dependent on platforms where ZInput performs its own system update.
    /// </summary>
    internal static string UpdateGridSelection(Player? player)
    {
        // A handled hotbar edge stays suppressible until the next ZInput update.
        // Whether Game.Update runs before or after Player.Update, the native
        // UseHotbarItem call therefore sees the same edge exactly once.
        suppressedHotbarIndex = 0;

        if (player == null || Player.m_localPlayer != player)
        {
            FarmingGridSelection.UpdatePickerSession(pickerOpen: false);
            return "not_local_player";
        }

        bool pickerOpen = IsCultivatorPieceSelectionOpen(player);
        if (FarmingGridSelection.UpdatePickerSession(pickerOpen))
        {
            PlantingPreview.DestroyGhosts();
        }

        // Text entry suppresses the shortcut without pretending the native
        // picker closed. Chat and other transient text fields can take focus
        // while the Cultivator picker remains visible.
        if (InputState.IsTextEntryActive())
        {
            return "text_entry";
        }

        bool leftShiftHeld = IsLeftShiftHeld();
        bool anotherModifierHeld = IsAnotherModifierHeld();
        if (!pickerOpen) return "picker_closed";
        if (!leftShiftHeld) return "left_shift_required";
        if (anotherModifierHeld) return "other_modifier";
        if (!TryGetPressedGridSize(out int number, out bool suppressHotbar, out string reason))
        {
            return reason;
        }

        suppressedHotbarIndex = suppressHotbar ? number : 0;

        FarmingGridSelection.TrySelect(number);

        PlantingPreview.DestroyGhosts();
        player.Message(MessageHud.MessageType.TopLeft, $"Planting grid: {number}x{number}");
        return "selected";
    }

    internal static bool ShouldSuppressHotbarUse(Player player, int index)
    {
        if (!localPlayerUpdateActive
            || Player.m_localPlayer != player
            || index != suppressedHotbarIndex)
        {
            return false;
        }

        suppressedHotbarIndex = 0;
        return true;
    }

    internal static void BeginPlayerUpdate(Player player)
    {
        localPlayerUpdateActive = Player.m_localPlayer == player;
    }

    internal static void EndPlayerUpdate()
    {
        localPlayerUpdateActive = false;
    }

    internal static void ResetGridSelection()
    {
        suppressedHotbarIndex = 0;
        localPlayerUpdateActive = false;
        FarmingGridSelection.Reset();
        FarmingInputDiagnostics.Reset();
    }

    private static bool IsCultivatorPieceSelectionOpen(Player player)
    {
        if (Player.m_localPlayer != player
            || !Hud.IsPieceSelectionVisible())
        {
            return false;
        }

        ItemDrop.ItemData? rightItem = player.RightItem;
        return rightItem != null
            && rightItem.m_dropPrefab != null
            && rightItem.m_dropPrefab.name == "Cultivator"
            && rightItem.m_shared.m_buildPieces != null
            && player.InPlaceMode();
    }

    private static bool TryGetPressedGridSize(out int number, out bool suppressHotbar, out string reason)
    {
        int activeNumberCount = 0;
        int activeTopRowNumber = -1;
        for (int candidate = 0; candidate <= 9; candidate++)
        {
            bool topRowActive = ZInput.GetKey((KeyCode)((int)KeyCode.Alpha0 + candidate));
            bool keypadActive = ZInput.GetKey((KeyCode)((int)KeyCode.Keypad0 + candidate));
            if (topRowActive)
            {
                activeNumberCount++;
                activeTopRowNumber = candidate;
            }

            if (keypadActive)
            {
                activeNumberCount++;
            }
        }

        bool allowedChord = activeNumberCount == 1
            && FarmingGridSelection.IsAllowed(activeTopRowNumber);
        bool selectionEdge = allowedChord && (activeTopRowNumber == 9
            ? ZInput.GetKeyDown(KeyCode.Alpha9)
            : ZInput.GetButtonDown($"Hotbar{activeTopRowNumber}"));

        bool handle = allowedChord && selectionEdge;
        number = handle ? activeTopRowNumber : 0;
        suppressHotbar = handle && activeTopRowNumber <= 8;
        reason = activeNumberCount != 1 ? "number_key_count"
            : !allowedChord ? "unsupported_number_key"
            : !selectionEdge ? "no_selection_edge" : "selected";
        return handle;
    }

    private static bool IsLeftShiftHeld()
    {
        if (InputState.IsTextEntryActive())
        {
            return false;
        }

        return ZInput.GetKey(KeyCode.LeftShift);
    }

    private static bool IsAnotherModifierHeld()
    {
        return ZInput.GetKey(KeyCode.RightShift)
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
