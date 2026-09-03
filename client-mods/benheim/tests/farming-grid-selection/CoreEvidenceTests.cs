using System;
using System.Linq;
using System.Text.Json;
using BenheimQoL.Farming;
using BenheimQoL.Infrastructure;
using UnityEngine;

internal static class CoreEvidenceTests
{
    internal static void Run(Player player)
    {
        FarmingInputProbe.TrySetActive(false, out _);
        FarmingInput.ResetGridSelection();
        player.RightItem = new ItemDrop.ItemData
        {
            m_dropPrefab = new GameObject("Cultivator"),
            m_shared = new ItemDrop.ItemData.SharedData { m_buildPieces = new PieceTable() },
        };
        Player.m_localPlayer = player;
        player.PlaceMode = true;
        Hud.PickerVisible = true;
        InputState.TextEntryActive = false;
        ZInput.Held.Clear();
        ZInput.ResetTransient();
        Input.Held.Clear();
        Input.KeyDown.Clear();
        Diagnostics.CoreEvents.Clear();

        Input.Held.Add(KeyCode.LeftShift);
        Input.KeyDown.Add(KeyCode.Alpha3);
        FarmingInputDiagnostics.ObserveUpdate();
        Require(Last().GetProperty("event").GetString() == "plant_grid_input" &&
            !Last().GetProperty("selection_seen_this_frame").GetBoolean() &&
            Last().GetProperty("legacy_top_down_mask").GetInt32() == 1 << 3 &&
            FarmingGridSelection.CurrentSize == 5,
            "ordinary core evidence records a raw-only attempt even when the selector hook never runs");
        for (int frame = 0; frame < 20; frame++)
        {
            Time.frameCount++;
            FarmingInputDiagnostics.ObserveUpdate();
        }
        Require(Diagnostics.CoreEvents.Count == 1, "a stale input edge cannot flood core evidence");
        Input.KeyDown.Clear();
        FarmingInputDiagnostics.ObserveUpdate();
        Input.KeyDown.Add(KeyCode.Alpha3);
        FarmingInputDiagnostics.ObserveUpdate();
        Require(Diagnostics.CoreEvents.Count == 2, "release rearms a later press of the same key");
        Input.KeyDown.Clear();
        Input.Held.Clear();

        // Reproduce the ordering that defeated an Update-only observer: no
        // selector callback, native hotbar use first, and then Plugin.Update
        // after the native equipment swap has replaced the Cultivator.
        FarmingInput.ResetGridSelection();
        ZInput.Held.Add(KeyCode.LeftShift);
        ZInput.ButtonDown.Add("Hotbar3");
        FarmingInputPatches.PlayerUpdatePrefix(player);
        Require(FarmingInputPatches.UseHotbarItemPrefix(player, 3),
            "without a selector decision the existing native hotbar path stays allowed");
        player.RightItem.m_dropPrefab = new GameObject("Hammer");
        FarmingInputPatches.PlayerUpdatePostfix();
        int beforeLateUpdate = Diagnostics.CoreEvents.Count;
        FarmingInputDiagnostics.ObserveUpdate();
        Require(Diagnostics.CoreEvents.Count == beforeLateUpdate &&
            Last().GetProperty("event").GetString() == "plant_grid_hotbar" &&
            Last().GetProperty("hotbar_index").GetInt32() == 3 &&
            !Last().GetProperty("selection_seen_this_frame").GetBoolean() &&
            !Last().GetProperty("suppressed").GetBoolean(),
            "the pre-swap observation survives the missing selector and later non-Cultivator Update");
        player.RightItem.m_dropPrefab = new GameObject("Cultivator");

        CheckDecision("selected", () => ZInput.ButtonDown.Add("Hotbar3"));
        Require(Last().GetProperty("selected_size").GetInt32() == 3,
            "default-on evidence records the actual successful selection");
        CheckDecision("no_selection_edge", () => ZInput.KeyDown.Add(KeyCode.Alpha3));
        CheckDecision("text_entry", () =>
        {
            InputState.TextEntryActive = true;
            ZInput.ButtonDown.Add("Hotbar7");
        });
        CheckDecision("other_modifier", () =>
        {
            ZInput.Held.Add(KeyCode.LeftControl);
            ZInput.ButtonDown.Add("Hotbar7");
        });
        CheckDecision("left_shift_required", () =>
        {
            ZInput.Held.Clear();
            ZInput.ButtonDown.Add("Hotbar7");
        });
        CheckDecision("picker_closed", () =>
        {
            Hud.PickerVisible = false;
            ZInput.ButtonDown.Add("Hotbar7");
        });
        CheckDecision("number_key_count", () =>
        {
            ZInput.ButtonDown.Add("Hotbar3");
            ZInput.ButtonDown.Add("Hotbar7");
        });
        CheckDecision("unsupported_number_key", () => ZInput.KeyDown.Add(KeyCode.Keypad3));

        ZInput.ResetTransient();
        FarmingInputPatches.ZInputUpdatePostfix(true);
        ZInput.ButtonDown.Add("Hotbar7");
        int selectedBefore = FarmingGridSelection.CurrentSize;
        FarmingInputPatches.ZInputUpdatePostfix(false);
        Require(Last().GetProperty("decision").GetString() == "native_update_skipped" &&
            FarmingGridSelection.CurrentSize == selectedBefore,
            "a skipped native update is observable without dispatching selection");

        player.RightItem.m_dropPrefab = new GameObject("Hammer");
        int count = Diagnostics.CoreEvents.Count;
        FarmingInputDiagnostics.ObserveUpdate();
        FarmingInputPatches.ZInputUpdatePostfix(true);
        Require(Diagnostics.CoreEvents.Count == count, "unrelated tool input is not recorded");

        Diagnostics.CoreEvents.Clear();
        PlantingDiagnostics.ResetPreview();
        Time.realtimeSinceStartup = 0;
        string[] cells = Enumerable.Repeat("valid", 9).ToArray();
        cells[4] = "anchor";
        PlantingDiagnostics.Preview("Carrot", 3, 1f, cells);
        Require(Last().GetProperty("grid_size").GetInt32() == 3 &&
            Last().GetProperty("cells").GetInt32() == 9 &&
            Last().GetProperty("valid").GetInt32() == 8,
            "preview evidence describes the consumed grid and excludes its native anchor from extra validity");
        cells[0] = "blocked_grow_space";
        for (int frame = 0; frame < 60; frame++) PlantingDiagnostics.Preview("Carrot", 3, 1f, cells);
        Require(Diagnostics.CoreEvents.Count == 1, "preview movement coalesces within the reporting interval");
        Time.realtimeSinceStartup = 1.1f;
        PlantingDiagnostics.Preview("Carrot", 3, 1f, cells);
        Require(Last().GetProperty("invalid").GetInt32() == 1 &&
            Last().GetProperty("cell_reasons").GetString()!.StartsWith("blocked_grow_space,", StringComparison.Ordinal),
            "a changed preview retains its reason and cell identity in one record");
        Time.realtimeSinceStartup = 10f;
        PlantingDiagnostics.Preview("Carrot", 3, 1f, cells);
        Require(Diagnostics.CoreEvents.Count == 2, "unchanged preview has no heartbeat spam");
        PlantingDiagnostics.Preview("Carrot", 5, 1f, Enumerable.Repeat("valid", 25).ToArray());
        Require(Diagnostics.CoreEvents.Count == 3 && Last().GetProperty("grid_size").GetInt32() == 5,
            "changing the selected grid bypasses preview coalescing");
        PlantingDiagnostics.PlacementFinished(5, 7, 2, 3, "insufficient_resources", 13);
        Require(Last().GetProperty("grid_size").GetInt32() == 5 &&
            Last().GetProperty("planted").GetInt32() == 8 &&
            Last().GetProperty("stopped_index").GetInt32() == 13,
            "placement evidence keeps the captured grid, completed placements, and stopping position");

        void CheckDecision(string expected, Action input)
        {
            ZInput.ResetTransient();
            ZInput.Held.Clear();
            ZInput.Held.Add(KeyCode.LeftShift);
            InputState.TextEntryActive = false;
            Hud.PickerVisible = true;
            Time.frameCount++;
            FarmingInputPatches.ZInputUpdatePostfix(true);
            int before = FarmingGridSelection.CurrentSize;
            input();
            FarmingInputPatches.ZInputUpdatePostfix(true);
            Require(Last().GetProperty("decision").GetString() == expected,
                $"the actual selection boundary must report {expected}");
            if (expected != "selected")
            {
                Require(FarmingGridSelection.CurrentSize == before &&
                    Last().GetProperty("event").GetString() == "plant_grid_selection_blocked",
                    "blocked evidence must agree with unchanged selection behavior");
            }
        }
    }

    private static JsonElement Last() => JsonDocument.Parse(Diagnostics.CoreEvents.Last().ToJsonLine()).RootElement;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
