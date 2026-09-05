using System;
using System.Linq;
using System.Text.Json;
using BenheimQoL.Farming;
using BenheimQoL.Infrastructure;
using UnityEngine;

Player player = CultivatorPlayer();
Player.m_localPlayer = player;
Hud.instance = new Hud();
Hud.PickerVisible = true;
FarmingGridPicker.Reset();
FarmingGridPicker.Update();
FarmingGridPickerView first = FarmingGridPickerView.Last!;
Require(first.IsAlive && first.HighlightedSize == 5 && FarmingGridSelection.CurrentSize == 5,
    "opening creates the row and highlights the default 5x5 choice");
Require(Last().GetProperty("result").GetString() == "shown" && Last().GetProperty("row_visible").GetBoolean(),
    "default-on evidence records the shown row");
int events = Diagnostics.CoreEvents.Count;
int creates = FarmingGridPickerView.CreateCount;
for (int i = 0; i < 60; i++) FarmingGridPicker.Update();
Require(Diagnostics.CoreEvents.Count == events && FarmingGridPickerView.CreateCount == creates,
    "unchanged picker frames neither allocate duplicate rows nor repeat state evidence");

foreach (int size in new[] { 1, 3, 5, 7, 9 })
{
    int invalidations = PlantingPreview.DestroyCalls;
    first.Click(size);
    Require(FarmingGridSelection.CurrentSize == size && first.HighlightedSize == size && Hud.PickerVisible,
        "every allowed click selects and highlights its size without closing the native picker");
    Require(PlantingPreview.DestroyCalls == invalidations + 1,
        "selection invalidates the prior preview");
    Require(Last().GetProperty("result").GetString() == "selected" &&
        Last().GetProperty("highlighted_size").GetInt32() == size &&
        Last().GetProperty("picker_visible").GetBoolean(),
        "selection result records actual state after applying the choice");
    Require(Diagnostics.CoreEvents[^2].Name == "plant_grid_choice_attempt",
        "each result has a preceding click attempt even for repeated choices");
}
foreach (int size in new[] { -1, 0, 2, 4, 6, 8, 10, 11 })
{
    first.Click(size);
    Require(FarmingGridSelection.CurrentSize == 9 && Reason() == "unsupported_size", "invalid choices do not mutate state");
}
InputState.TextEntryActive = true;
FarmingGridPicker.Update();
first.Click(3);
Require(first.IsAlive && FarmingGridSelection.CurrentSize == 9 && Reason() == "text_entry",
    "transient console or chat focus blocks a click without resetting its still-open picker");
InputState.TextEntryActive = false;

Hud.PickerVisible = false;
FarmingGridPicker.Update();
Require(!first.IsAlive && FarmingGridSelection.CurrentSize == 9,
    "closing removes the controls while preserving size for Shift preview and placement");
first.Click(3);
Require(FarmingGridSelection.CurrentSize == 9 && Reason() == "picker_closed", "closed picker callbacks cannot select");
Hud.PickerVisible = true;
FarmingGridPicker.Update();
FarmingGridPickerView second = FarmingGridPickerView.Last!;
Require(second != first && FarmingGridSelection.CurrentSize == 5 && second.HighlightedSize == 5,
    "every reopening creates a fresh default-5 session");
first.Click(9);
Require(FarmingGridSelection.CurrentSize == 5 && Reason() == "stale_picker",
    "a retained callback from the prior row cannot mutate a new same-player session");
second.Click(3);

// Check the same boundary again at click time, before Update sees a tool swap.
player.RightItem!.m_dropPrefab = new GameObject("Hammer");
second.Click(7);
Require(FarmingGridSelection.CurrentSize == 3 && Reason() == "other_tool", "equipment changes block stale clicks immediately");
FarmingGridPicker.Update();
Require(!second.IsAlive, "Hammer never retains the Cultivator row");
events = Diagnostics.CoreEvents.Count;
for (int i = 0; i < 60; i++) FarmingGridPicker.Update();
Require(Diagnostics.CoreEvents.Count == events, "unrelated tool play does not emit picker spam");
player.RightItem.m_dropPrefab = new GameObject("Cultivator");
player.PlaceMode = false;
FarmingGridPicker.Update();
Require(FarmingGridPickerView.Last == second, "Cultivator outside place mode does not get a row");
player.PlaceMode = true;
player.RightItem.m_shared.m_buildPieces = null;
FarmingGridPicker.Update();
Require(FarmingGridPickerView.Last == second, "a tool without build pieces does not get a row");
player.RightItem.m_shared.m_buildPieces = new PieceTable();
FarmingGridPicker.Update();
FarmingGridPickerView third = FarmingGridPickerView.Last!;
Require(third != second && third.HighlightedSize == 5, "returning to the Cultivator starts at 5");
third.Click(7);
Player.m_localPlayer = CultivatorPlayer();
FarmingGridPicker.Update();
Require(!third.IsAlive && FarmingGridSelection.CurrentSize == 5, "player replacement cleans the old owner and resets choice");
FarmingGridPickerView current = FarmingGridPickerView.Last!;
Player.m_localPlayer = null;
FarmingGridPicker.Update();
Require(!current.IsAlive, "world exit/player absence removes the row");
Player.m_localPlayer = player;
FarmingGridPicker.Update();
current = FarmingGridPickerView.Last!;
HealthReporting.GameplayActionsEnabled = false;
FarmingGridPicker.Update();
Require(!current.IsAlive && Reason() == "gameplay_disabled", "disabled gameplay cleans controls before Plugin returns early");
HealthReporting.GameplayActionsEnabled = true;

FarmingGridPickerView.MissingReason = "native_button_missing";
FarmingGridPicker.Update();
Require(Reason() == "native_button_missing" && !Last().GetProperty("row_visible").GetBoolean(),
    "a missing donor reports unavailable rather than claiming a shown row");
events = Diagnostics.CoreEvents.Count;
creates = FarmingGridPickerView.CreateCount;
for (int i = 0; i < 60; i++) FarmingGridPicker.Update();
Require(FarmingGridPickerView.CreateCount == creates, "missing donors retry at a bounded rate");
Time.realtimeSinceStartup += 2;
FarmingGridPicker.Update();
Require(Diagnostics.CoreEvents.Count == events, "unchanged missing-donor outcome is emitted only once");
FarmingGridPickerView.MissingReason = string.Empty;
Time.realtimeSinceStartup += 2;
FarmingGridPicker.Update();
Require(FarmingGridPickerView.Last!.IsAlive && Last().GetProperty("result").GetString() == "shown",
    "late native donors recover through the same session without a new command");
current = FarmingGridPickerView.Last!;
current.ThrowOnHighlight = true;
current.Click(9);
Require(Last().GetProperty("result").GetString() == "failed" &&
    Last().GetProperty("selected_size").GetInt32() == 9 &&
    Last().GetProperty("highlighted_size").GetInt32() == 5,
    "a rendering operation failure records the actual partial state, not a fabricated selected highlight");
current.ThrowOnHighlight = false;
Diagnostics.ThrowOnEmit = true;
current.Click(3);
Require(FarmingGridSelection.CurrentSize == 3 && current.HighlightedSize == 3,
    "a throwing diagnostic sink cannot escape the click or interrupt selection");
FarmingGridPicker.Reset();
Require(!current.IsAlive && FarmingGridSelection.CurrentSize == 5, "reset cleans the row even with a failed sink");
Diagnostics.ThrowOnEmit = false;
FarmingGridPickerView.ThrowOnCreate = true;
FarmingGridPicker.Update();
Require(Last().GetProperty("result").GetString() == "failed", "creation exceptions are contained and recorded");
FarmingGridPickerView.ThrowOnCreate = false;
Time.realtimeSinceStartup += 2;
FarmingGridPicker.Update();
Require(FarmingGridPickerView.Last!.IsAlive, "creation failure can recover when the donor becomes usable");
FarmingGridPicker.Reset();

ZInput.Held.Add(KeyCode.LeftShift);
Require(FarmingInput.IsMassActionHeld(), "the existing Shift planting/harvesting control remains available");
InputState.TextEntryActive = true;
Require(!FarmingInput.IsMassActionHeld(), "text entry still suppresses mass actions");
InputState.TextEntryActive = false;
ZInput.Held.Clear();
Input.Held.Add(KeyCode.JoystickButton4);
Require(FarmingInput.IsMassActionHeld(), "the existing gamepad mass action is preserved");
CoreEvidenceTests.Run();
Console.WriteLine("farming picker session and core evidence checks passed");

static Player CultivatorPlayer() => new Player
{
    PlaceMode = true,
    RightItem = new ItemDrop.ItemData
    {
        m_dropPrefab = new GameObject("Cultivator"),
        m_shared = new ItemDrop.ItemData.SharedData { m_buildPieces = new PieceTable() },
    },
};
static JsonElement Last() => JsonDocument.Parse(Diagnostics.CoreEvents.Last().ToJsonLine()).RootElement;
static string? Reason() => Last().GetProperty("reason").GetString();
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
