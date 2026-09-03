using System;
using BenheimQoL.DeveloperDiagnostics;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Farming;

// One short, opt-in capture of the failing Cultivator shortcut. Registry Update
// is deliberately independent of the ZInput hook whose liveness is in doubt.
internal static class FarmingInputProbe
{
    internal const string Name = "gridinput";
    internal const float DurationSeconds = 45f;
    internal const int MaximumRecords = 64;
    private static bool active;
    private static bool captureStarted;
    private static float startedAt;
    private static float nextHeartbeat;
    private static int updates;
    private static int postfixEntries;
    private static int originalsRun;
    private static int records;
    private static string? stopReason;

    internal static bool TrySetActive(bool requested, out string failure)
    {
        failure = string.Empty;
        active = requested;
        if (requested)
        {
            captureStarted = true;
            startedAt = Time.realtimeSinceStartup;
            nextHeartbeat = startedAt;
            updates = postfixEntries = originalsRun = records = 0;
            stopReason = null;
        }
        return true;
    }

    internal static void Update()
    {
        if (!CanRecord()) return;
        updates++;
        Observe("plugin_update", heartbeat: Time.realtimeSinceStartup >= nextHeartbeat);
    }

    internal static void ObserveZInputPostfix(bool originalRan)
    {
        // These callbacks run in gameplay hooks, outside the registry's Update
        // exception boundary. A broken observer must never break selection.
        try
        {
            if (!CanRecord()) return;
            postfixEntries++;
            if (originalRan) originalsRun++;
            Observe(originalRan ? "zinput_postfix" : "zinput_original_skipped",
                heartbeat: postfixEntries == 1);
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    internal static void ObserveHotbarUse(Player player, int index, bool suppressed)
    {
        try
        {
            if (!CanRecord()) return;
            Observe("use_hotbar_item", heartbeat: true,
                index, suppressed, Player.m_localPlayer == player);
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    internal static void Cleanup(DiagnosticProbeCleanupReason reason)
    {
        active = false;
        if (!captureStarted) return;
        captureStarted = false;
        if (reason == DiagnosticProbeCleanupReason.WorldExit ||
            reason == DiagnosticProbeCleanupReason.Failure)
        {
            DeveloperDiagnosticsRuntime.DisableEventProbe(Name);
        }
        Diagnostics.Emit(Counters("grid_input_probe_complete")
            .String("reason", stopReason ?? reason.ToString())
            .Integer("snapshot_records", records));
    }

    private static bool CanRecord()
    {
        if (!active) return false;
        if (Time.realtimeSinceStartup - startedAt < DurationSeconds) return true;
        Finish("timeout");
        return false;
    }

    private static void Observe(
        string seam, bool heartbeat,
        int hotbarUseIndex = 0, bool suppressed = false, bool localHotbarPlayer = false)
    {
        int topHeld = 0, keypadHeld = 0, topDown = 0, keypadDown = 0, hotbarDown = 0;
        int legacyTopHeld = 0, legacyTopDown = 0;
        for (int digit = 0; digit <= 9; digit++)
        {
            int bit = 1 << digit;
            KeyCode top = (KeyCode)((int)KeyCode.Alpha0 + digit);
            KeyCode keypad = (KeyCode)((int)KeyCode.Keypad0 + digit);
            if (ZInput.GetKey(top)) topHeld |= bit;
            if (ZInput.GetKey(keypad)) keypadHeld |= bit;
            if (ZInput.GetKeyDown(top)) topDown |= bit;
            if (ZInput.GetKeyDown(keypad)) keypadDown |= bit;
            if (digit >= 1 && digit <= 8 && ZInput.GetButtonDown($"Hotbar{digit}")) hotbarDown |= bit;
            if (Input.GetKey(top)) legacyTopHeld |= bit;
            if (Input.GetKeyDown(top)) legacyTopDown |= bit;
        }

        // Capture latched edges here, before a later Update can lose them.
        // Repeated native edges remain separate observations, bounded by the
        // record cap; this preserves evidence of ordering across both seams.
        if (!heartbeat && (topDown | keypadDown | hotbarDown | legacyTopDown) == 0) return;
        if (seam == "plugin_update" && heartbeat)
            nextHeartbeat = Time.realtimeSinceStartup + 5f;
        EmitSnapshot(seam, topHeld, keypadHeld, topDown, keypadDown, hotbarDown,
            legacyTopHeld, legacyTopDown, hotbarUseIndex, suppressed, localHotbarPlayer);
    }

    private static void EmitSnapshot(
        string seam, int topHeld, int keypadHeld, int topDown, int keypadDown, int hotbarDown,
        int legacyTopHeld, int legacyTopDown,
        int hotbarUseIndex = 0, bool suppressed = false, bool localHotbarPlayer = false)
    {
        // Reserve the last record for completion, including the stop reason.
        if (records >= MaximumRecords - 1)
        {
            Finish("record_limit");
            return;
        }
        records++;
        Player? player = Player.m_localPlayer;
        ItemDrop.ItemData? tool = player?.RightItem;
        Diagnostics.Emit(Counters("grid_input_probe")
            .String("seam", seam)
            .Integer("record", records)
            .Integer("frame", Time.frameCount)
            .Boolean("player_present", player != null)
            .Boolean("picker_visible", Hud.IsPieceSelectionVisible())
            .String("tool_prefab", tool?.m_dropPrefab == null ? "none" : tool.m_dropPrefab.name)
            .Boolean("tool_has_build_pieces", tool?.m_shared.m_buildPieces != null)
            .Boolean("place_mode", player != null && player.InPlaceMode())
            .Boolean("text_entry", InputState.IsTextEntryActive())
            .Boolean("left_shift", ZInput.GetKey(KeyCode.LeftShift))
            .Boolean("legacy_left_shift", Input.GetKey(KeyCode.LeftShift))
            .Boolean("legacy_right_shift", Input.GetKey(KeyCode.RightShift))
            .Boolean("right_shift", ZInput.GetKey(KeyCode.RightShift))
            .Boolean("left_alt", ZInput.GetKey(KeyCode.LeftAlt))
            .Boolean("right_alt", ZInput.GetKey(KeyCode.RightAlt))
            .Boolean("left_control", ZInput.GetKey(KeyCode.LeftControl))
            .Boolean("right_control", ZInput.GetKey(KeyCode.RightControl))
            .Boolean("alt_gr", ZInput.GetKey(KeyCode.AltGr))
            .Boolean("left_command", ZInput.GetKey(KeyCode.LeftCommand))
            .Boolean("right_command", ZInput.GetKey(KeyCode.RightCommand))
            .Boolean("left_meta", ZInput.GetKey(KeyCode.LeftMeta))
            .Boolean("right_meta", ZInput.GetKey(KeyCode.RightMeta))
            .Boolean("left_windows", ZInput.GetKey(KeyCode.LeftWindows))
            .Boolean("right_windows", ZInput.GetKey(KeyCode.RightWindows))
            // Bit n identifies digit n. No arbitrary text or keys are recorded.
            .Integer("top_held_mask", topHeld)
            .Integer("keypad_held_mask", keypadHeld)
            .Integer("top_down_mask", topDown)
            .Integer("keypad_down_mask", keypadDown)
            .Integer("hotbar_down_mask", hotbarDown)
            .Integer("legacy_top_held_mask", legacyTopHeld)
            .Integer("legacy_top_down_mask", legacyTopDown)
            .Integer("selected_size", FarmingGridSelection.CurrentSize)
            .Integer("hotbar_use_index", hotbarUseIndex)
            .Boolean("hotbar_suppressed", suppressed)
            .Boolean("hotbar_player_local", localHotbarPlayer));
    }

    private static DiagnosticEvent Counters(string name) =>
        DiagnosticEvent.Create("Farming", name)
            .Number("elapsed_seconds", Time.realtimeSinceStartup - startedAt)
            .Integer("plugin_updates", updates)
            .Integer("zinput_postfix_entries", postfixEntries)
            .Integer("zinput_originals_run", originalsRun);

    private static void Finish(string reason)
    {
        stopReason = reason;
        DeveloperDiagnosticsRuntime.DisableEventProbe(Name);
    }

    private static void Fail(Exception exception)
    {
        try
        {
            DeveloperDiagnosticsRuntime.ReportFailure("input_observation", Name, exception.Message);
            Finish("observation_failed");
        }
        catch
        {
            // Even failure reporting is observational. A broken sink must not
            // escape the Harmony callback into the player's input handling.
        }
    }
}
