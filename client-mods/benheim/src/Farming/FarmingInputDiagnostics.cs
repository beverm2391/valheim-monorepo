using System;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Farming;

// Core evidence is always on. The separate gridinput probe owns verbose,
// time-bounded inspection; this owner records only relevant number-key edges.
internal static class FarmingInputDiagnostics
{
    private static long lastUpdateEdges;
    private static long lastSelectionEdges;
    private static int selectionFrame = -1;
    private static string selectionDecision = "not_observed";
    private static bool failureReported;

    internal static void ObserveUpdate() => Observe(selection: false);

    internal static void ObserveHotbarUse(Player player, int index, bool suppressed)
    {
        // Native hotbar use may replace the Cultivator before Plugin.Update.
        // Capture at the existing prefix while the relevant tool is still held.
        if (player == Player.m_localPlayer) Observe(selection: false, index, suppressed);
    }

    internal static void ObserveSelection(string decision)
    {
        selectionFrame = Time.frameCount;
        selectionDecision = decision;
        Observe(selection: true);
    }

    internal static void Reset()
    {
        lastUpdateEdges = lastSelectionEdges = 0;
        selectionFrame = -1;
        selectionDecision = "not_observed";
        failureReported = false;
    }

    private static void Observe(bool selection, int hotbarUseIndex = 0, bool suppressed = false)
    {
        try
        {
            Player? player = Player.m_localPlayer;
            ItemDrop.ItemData? tool = player?.RightItem;
            bool cultivator = tool?.m_dropPrefab != null && tool.m_dropPrefab.name == "Cultivator";
            bool pickerVisible = cultivator && Hud.IsPieceSelectionVisible();
            bool shift = cultivator && (ZInput.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.LeftShift));
            if (!cultivator || (!pickerVisible && !shift))
            {
                SetLastEdges(selection, 0);
                return;
            }

            int topDown = 0, keypadDown = 0, hotbarDown = 0, legacyDown = 0;
            for (int digit = 0; digit <= 9; digit++)
            {
                int bit = 1 << digit;
                KeyCode top = (KeyCode)((int)KeyCode.Alpha0 + digit);
                if (ZInput.GetKeyDown(top)) topDown |= bit;
                if (ZInput.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + digit))) keypadDown |= bit;
                if (digit >= 1 && digit <= 8 && ZInput.GetButtonDown($"Hotbar{digit}")) hotbarDown |= bit;
                if (Input.GetKeyDown(top)) legacyDown |= bit;
            }

            long edges = (uint)topDown | ((long)keypadDown << 10) | ((long)hotbarDown << 20) | ((long)legacyDown << 30);
            long previous = selection ? lastSelectionEdges : lastUpdateEdges;
            bool hotbarUse = hotbarUseIndex != 0;
            if (!hotbarUse) SetLastEdges(selection, edges);
            // A stale latched edge must not produce a record every frame. Each
            // seam becomes eligible again after observing release/no edge.
            if (!hotbarUse && (edges == 0 || edges == previous)) return;

            string name = hotbarUse ? "plant_grid_hotbar" : !selection ? "plant_grid_input"
                : selectionDecision == "selected" ? "plant_grid_selected" : "plant_grid_selection_blocked";
            DiagnosticEvent record = DiagnosticEvent.Create("Farming", name)
                .String("observation", hotbarUse ? "use_hotbar_item" : selection ? "selection_handler" : "plugin_update")
                .String("decision", hotbarUse ? suppressed ? "native_hotbar_suppressed" : "native_hotbar_allowed"
                    : selection ? selectionDecision : "input_observed")
                .Integer("frame", Time.frameCount)
                .Integer("selection_frame", selectionFrame)
                .Boolean("selection_seen_this_frame", selectionFrame == Time.frameCount)
                .Boolean("picker_visible", pickerVisible)
                .Boolean("place_mode", player!.InPlaceMode())
                .Boolean("tool_has_build_pieces", tool!.m_shared.m_buildPieces != null)
                .Boolean("text_entry", InputState.IsTextEntryActive())
                .Boolean("left_shift", ZInput.GetKey(KeyCode.LeftShift))
                .Boolean("legacy_left_shift", Input.GetKey(KeyCode.LeftShift))
                .Integer("top_down_mask", topDown)
                .Integer("keypad_down_mask", keypadDown)
                .Integer("hotbar_down_mask", hotbarDown)
                .Integer("legacy_top_down_mask", legacyDown)
                .Integer("selected_size", FarmingGridSelection.CurrentSize);
            if (hotbarUse) record.Integer("hotbar_index", hotbarUseIndex).Boolean("suppressed", suppressed);
            Diagnostics.Emit(record);
        }
        catch (Exception exception)
        {
            // Observing input must not alter selection or native hotbar use,
            // even if a Unity input backend or the diagnostic sink fails.
            if (failureReported) return;
            failureReported = true;
            try { Plugin.Log.LogWarning($"Farming input evidence unavailable: {exception.GetType().Name}"); }
            catch { }
        }
    }

    private static void SetLastEdges(bool selection, long value)
    {
        if (selection) lastSelectionEdges = value;
        else lastUpdateEdges = value;
    }
}
