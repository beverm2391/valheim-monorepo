using System;
using BenheimQoL.Farming;
using BenheimQoL.Infrastructure;
using UnityEngine;

Player player = CultivatorPlayer();
Player.m_localPlayer = player;
Hud.PickerVisible = true;

FarmingInput.ResetGridSelection();
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "opening the picker must start at 9x9");

// Valheim 0.221.12 latches Hotbar1-8 during ZInput.Update. The production
// boundary must use that latched button, not the lower-level key edge that can
// be updated later than Player.Update.
ZInput.Held.Add(KeyCode.LeftShift);
ZInput.KeyDown.Add(KeyCode.Alpha3);
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "a raw top-row edge must not bypass the native hotbar boundary");
Require(Diagnostics.Events == 0, "the rejected raw edge must not emit selection evidence");

ZInput.ResetTransient();
ZInput.ButtonDown.Add("Hotbar3");
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 3, "Left Shift+3 must select the 3x3 grid");
Require(player.LastMessage == "Planting grid: 3x3", "selection must show immediate top-left confirmation");
Require(Diagnostics.Last == "Farming.plant_grid_selected grid=3x3", "selection must emit the typed event");
int selectedEventCount = Diagnostics.Events;
ZInput.ResetTransient();
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: false);
Require(Diagnostics.Events == selectedEventCount, "a skipped ZInput.Update must not replay stale selection input");
FarmingInputPatches.PlayerUpdatePrefix(player);
Exception updateFailure = new Exception("simulated Player.Update failure");
Require(ReferenceEquals(FarmingInputPatches.PlayerUpdateFinalizer(updateFailure), updateFailure), "the Player.Update finalizer must preserve native exceptions");
Require(FarmingInputPatches.UseHotbarItemPrefix(player, 3), "a matching call outside native Player.Update must not consume suppression");
Require(!NativeHotbarPatchAllows(player, 3), "the matching native hotbar action must be suppressed");
Require(NativeHotbarPatchAllows(player, 3), "the suppression token must be single-use");

foreach (int size in new[] { 1, 3, 5, 7 })
{
    ZInput.ResetTransient();
    ZInput.ButtonDown.Add($"Hotbar{size}");
    FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
    Require(FarmingGridSelection.CurrentSize == size, $"Left Shift+{size} must select {size}x{size}");
    Require(!NativeHotbarPatchAllows(player, size), $"Hotbar{size} must be consumed");
}

ZInput.ResetTransient();
ZInput.KeyDown.Add(KeyCode.Alpha9);
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "Left Shift+9 must use the raw 9-key seam");
Require(NativeHotbarPatchAllows(player, 9), "9 has no native Valheim hotbar action to suppress");

ZInput.ResetTransient();
ZInput.KeyDown.Add(KeyCode.Keypad5);
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "keypad input must remain native");
Require(NativeHotbarPatchAllows(player, 5), "keypad input must not create a native hotbar suppression token");

ZInput.ResetTransient();
ZInput.ButtonDown.Add("Hotbar7");
ZInput.Held.Add(KeyCode.LeftControl);
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "another modifier must preserve the current selection");
Require(NativeHotbarPatchAllows(player, 7), "a rejected chord must remain native");

ZInput.ResetTransient();
ZInput.Held.Clear();
ZInput.ButtonDown.Add("Hotbar1");
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "plain number input must remain native");

ZInput.ResetTransient();
ZInput.Held.Add(KeyCode.LeftShift);
ZInput.Held.Add(KeyCode.RightShift);
ZInput.ButtonDown.Add("Hotbar7");
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "Right Shift must reject a Left Shift chord");
Require(NativeHotbarPatchAllows(player, 7), "the rejected dual-shift chord must remain native");

ZInput.ResetTransient();
ZInput.Held.Remove(KeyCode.RightShift);
ZInput.ButtonDown.Add("Hotbar2");
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "even number input must remain native");
Require(NativeHotbarPatchAllows(player, 2), "an even hotbar action must not be consumed");

ZInput.ResetTransient();
ZInput.ButtonDown.Add("Hotbar1");
ZInput.ButtonDown.Add("Hotbar3");
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "simultaneous number keys must remain native");
Require(NativeHotbarPatchAllows(player, 1), "the first simultaneous hotbar action must not be consumed");
Require(NativeHotbarPatchAllows(player, 3), "the second simultaneous hotbar action must not be consumed");

ZInput.ResetTransient();
ZInput.KeyDown.Add(KeyCode.Alpha0);
ZInput.ButtonDown.Add("Hotbar3");
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "top-row 0 plus an allowed digit must remain native");
Require(NativeHotbarPatchAllows(player, 3), "a hotbar action paired with top-row 0 must not be consumed");

ZInput.ResetTransient();
ZInput.Held.Add(KeyCode.Alpha2);
ZInput.ButtonDown.Add("Hotbar3");
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "a held top-row digit plus a newly pressed allowed digit must remain native");
Require(NativeHotbarPatchAllows(player, 3), "a hotbar action paired with a held top-row digit must not be consumed");
ZInput.Held.Remove(KeyCode.Alpha2);

ZInput.ResetTransient();
ZInput.Held.Add(KeyCode.Keypad5);
ZInput.ButtonDown.Add("Hotbar3");
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "a held keypad digit plus a newly pressed allowed digit must remain native");
Require(NativeHotbarPatchAllows(player, 3), "a hotbar action paired with a held keypad digit must not be consumed");
ZInput.Held.Remove(KeyCode.Keypad5);

Hud.PickerVisible = false;
ZInput.ResetTransient();
ZInput.ButtonDown.Add("Hotbar1");
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "picker-closed input must remain native");
Require(NativeHotbarPatchAllows(player, 1), "picker-closed hotbar input must not be consumed");
Hud.PickerVisible = true;
ZInput.ResetTransient();
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "reopening after the picker-closed check must reset to 9x9");

ZInput.ResetTransient();
ZInput.Held.Add(KeyCode.LeftShift);
ZInput.ButtonDown.Add("Hotbar3");
Require(NativeHotbarPatchAllows(player, 3), "Player.Update before ZInput.Update must see no premature suppression");
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 3, "the test setup must create a pending suppression");
Require(!NativeHotbarPatchAllows(player, 3), "the next Player.Update must consume a token created by later ZInput.Update");

ZInput.ResetTransient();
ZInput.ButtonDown.Add("Hotbar3");
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 3, "the test setup must recreate a pending suppression");
ZInput.ResetTransient();
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(NativeHotbarPatchAllows(player, 3), "an unused suppression must expire at the next ZInput update");

Hud.PickerVisible = false;
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Hud.PickerVisible = true;
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "each picker session must reset to 9x9");

player.RightItem = Item("Hammer");
ZInput.ResetTransient();
ZInput.Held.Add(KeyCode.LeftShift);
ZInput.ButtonDown.Add("Hotbar1");
FarmingInputPatches.ZInputUpdatePostfix(__runOriginal: true);
Require(FarmingGridSelection.CurrentSize == 9, "another tool's picker must remain native");

Console.WriteLine("farming production input boundary checks passed");

static Player CultivatorPlayer() => new Player
{
    RightItem = Item("Cultivator"),
    PlaceMode = true,
};

static ItemDrop.ItemData Item(string prefabName) => new ItemDrop.ItemData
{
    m_dropPrefab = new GameObject(prefabName),
    m_shared = new ItemDrop.ItemData.SharedData
    {
        m_buildPieces = new PieceTable(),
    },
};

static bool NativeHotbarPatchAllows(Player player, int index)
{
    FarmingInputPatches.PlayerUpdatePrefix(player);
    try
    {
        return FarmingInputPatches.UseHotbarItemPrefix(player, index);
    }
    finally
    {
        FarmingInputPatches.PlayerUpdatePostfix();
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
