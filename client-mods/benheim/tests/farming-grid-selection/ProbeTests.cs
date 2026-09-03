using System;
using System.Linq;
using System.Text.Json;
using BenheimQoL.DeveloperDiagnostics;
using BenheimQoL.Farming;
using BenheimQoL.Infrastructure;
using UnityEngine;

internal static class ProbeTests
{
    internal static void Run()
    {
        Player player = new()
        {
            PlaceMode = true,
            RightItem = new ItemDrop.ItemData
            {
                m_dropPrefab = new GameObject("Cultivator"),
                m_shared = new ItemDrop.ItemData.SharedData { m_buildPieces = new PieceTable() },
            },
        };
        Player.m_localPlayer = player;
        Hud.PickerVisible = true;
        InputState.TextEntryActive = false;
        ZInput.Held.Clear();
        ZInput.ResetTransient();
        Diagnostics.ProbeEvents.Clear();

        FarmingInputProbe.Update();
        FarmingInputProbe.ObserveZInputPostfix(true);
        Require(Diagnostics.ProbeEvents.Count == 0, "a default-off probe produces no evidence");

        BeginCapture();
        FarmingInputProbe.Update();
        JsonElement independent = Last();
        Require(independent.GetProperty("plugin_updates").GetInt32() == 1 &&
            independent.GetProperty("zinput_postfix_entries").GetInt32() == 0,
            "the independent update heartbeat must expose a missing ZInput hook");

        ZInput.Held.Add(KeyCode.LeftShift);
        ZInput.ButtonDown.Add("Hotbar3");
        FarmingInputPatches.ZInputUpdatePostfix(true);
        JsonElement edge = Last();
        Require(edge.GetProperty("seam").GetString() == "zinput_postfix" &&
            edge.GetProperty("hotbar_down_mask").GetInt32() == 1 << 3 &&
            edge.GetProperty("top_held_mask").GetInt32() == 1 << 3 &&
            edge.GetProperty("left_shift").GetBoolean() &&
            edge.GetProperty("tool_prefab").GetString() == "Cultivator" &&
            edge.GetProperty("picker_visible").GetBoolean() &&
            edge.GetProperty("place_mode").GetBoolean(),
            "the actual postfix must capture the complete chord and picker context");
        FarmingInputPatches.PlayerUpdatePrefix(player);
        Require(!FarmingInputPatches.UseHotbarItemPrefix(player, 3),
            "observing the edge must not consume its native suppression token");
        Require(Last().GetProperty("hotbar_suppressed").GetBoolean() &&
            Last().GetProperty("hotbar_down_mask").GetInt32() == 1 << 3,
            "the actual hotbar seam records its outcome and still-visible edge");
        FarmingInputPatches.PlayerUpdatePostfix();
        ZInput.ResetTransient();
        int captured = Diagnostics.ProbeEvents.Count;
        FarmingInputProbe.Update();
        Require(Diagnostics.ProbeEvents.Count == captured,
            "a later sample does not need to see the edge for the evidence to survive");

        Input.Held.Add(KeyCode.LeftShift);
        Input.KeyDown.Add(KeyCode.Alpha3);
        FarmingInputProbe.Update();
        Require(Last().GetProperty("seam").GetString() == "plugin_update" &&
            Last().GetProperty("legacy_top_down_mask").GetInt32() == 1 << 3 &&
            Last().GetProperty("top_down_mask").GetInt32() == 0 &&
            Last().GetProperty("hotbar_down_mask").GetInt32() == 0,
            "an independent legacy key edge must be recorded even when ZInput sees no edge");
        Input.Held.Clear();
        Input.KeyDown.Clear();

        ZInput.KeyDown.Add(KeyCode.Alpha9);
        FarmingInputPatches.ZInputUpdatePostfix(true);
        Require(Last().GetProperty("top_down_mask").GetInt32() == 1 << 9 &&
            Last().GetProperty("hotbar_down_mask").GetInt32() == 0,
            "9 records the raw key edge independently of Hotbar1 through Hotbar8");
        ZInput.ResetTransient();
        FarmingInputProbe.ObserveZInputPostfix(false);
        Time.realtimeSinceStartup = FarmingInputProbe.DurationSeconds;
        FarmingInputProbe.Update();
        Require(Last().GetProperty("reason").GetString() == "timeout" &&
            Last().GetProperty("zinput_postfix_entries").GetInt32() == 3 &&
            Last().GetProperty("zinput_originals_run").GetInt32() == 2,
            "timeout completes through the registry and preserves skipped-original counts");
        captured = Diagnostics.ProbeEvents.Count;
        FarmingInputProbe.ObserveZInputPostfix(true);
        Require(Diagnostics.ProbeEvents.Count == captured, "completed probes stop hook observation");

        BeginCapture();
        ZInput.KeyDown.Add(KeyCode.Alpha9);
        for (int index = 0; index < FarmingInputProbe.MaximumRecords + 5; index++)
            FarmingInputProbe.ObserveZInputPostfix(true);
        Require(Diagnostics.ProbeEvents.Count == FarmingInputProbe.MaximumRecords &&
            Last().GetProperty("reason").GetString() == "record_limit",
            "the record cap includes exactly one completion record");

        BeginCapture();
        int disablesBeforeWorldExit = DeveloperDiagnosticsRuntime.DisableCalls;
        FarmingInputProbe.Cleanup(DiagnosticProbeCleanupReason.WorldExit);
        FarmingInputProbe.Update();
        Require(Diagnostics.ProbeEvents.Count == 1 &&
            Last().GetProperty("reason").GetString() == "WorldExit" &&
            DeveloperDiagnosticsRuntime.DisableCalls == disablesBeforeWorldExit + 1,
            "early world exit ends the registry session and emits completion only once");

        BeginCapture();
        int disablesBeforeFailure = DeveloperDiagnosticsRuntime.DisableCalls;
        FarmingInputProbe.Cleanup(DiagnosticProbeCleanupReason.Failure);
        Require(Diagnostics.ProbeEvents.Count == 1 &&
            Last().GetProperty("reason").GetString() == "Failure" &&
            DeveloperDiagnosticsRuntime.DisableCalls == disablesBeforeFailure + 1,
            "registry failure cleanup clears the one-shot session before another world can load");

        BeginCapture();
        ZInput.ResetTransient();
        ZInput.ButtonDown.Add("Hotbar3");
        ZInput.ThrowOnKeyDown = true;
        FarmingInputPatches.ZInputUpdatePostfix(true);
        ZInput.ThrowOnKeyDown = false;
        Require(DeveloperDiagnosticsRuntime.Failures == 1 && FarmingGridSelection.CurrentSize == 3 &&
            Last().GetProperty("reason").GetString() == "observation_failed",
            "a throwing observer disables itself without escaping the hook or stopping selection");
        ZInput.ResetTransient();

        BeginCapture();
        ZInput.ButtonDown.Add("Hotbar3");
        Diagnostics.ThrowOnProbeEmit = true;
        FarmingInputPatches.ZInputUpdatePostfix(true);
        Require(FarmingGridSelection.CurrentSize == 3,
            "a throwing diagnostic sink must not escape the ZInput callback");
        BeginCapture();
        FarmingInputPatches.PlayerUpdatePrefix(player);
        Require(!FarmingInputPatches.UseHotbarItemPrefix(player, 3),
            "a throwing diagnostic sink must not escape or change native hotbar suppression");
        FarmingInputPatches.PlayerUpdatePostfix();
        Diagnostics.ThrowOnProbeEmit = false;
        ZInput.ResetTransient();
    }

    private static void BeginCapture()
    {
        Diagnostics.ProbeEvents.Clear();
        Time.realtimeSinceStartup = 0f;
        Time.frameCount++;
        FarmingInputProbe.TrySetActive(true, out _);
    }

    private static JsonElement Last() =>
        JsonDocument.Parse(Diagnostics.ProbeEvents.Last().ToJsonLine()).RootElement;

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
