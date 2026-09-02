using System;
using BenheimQoL.Farming;

FarmingGridSelection.Reset();
Require(FarmingGridSelection.CurrentSize == 9, "reset must restore 9x9");
Require(FarmingGridSelection.UpdatePickerSession(pickerOpen: true), "opening the picker must begin a selection session");
Require(FarmingGridSelection.TrySelect(3), "the open picker session must allow a selection");
Require(FarmingGridSelection.CurrentSize == 3, "the open picker session must retain its selection");
Require(
    !FarmingGridSelection.UpdatePickerSession(pickerOpen: true),
    "text focus while the picker remains open must not restart its session");
Require(
    FarmingGridSelection.CurrentSize == 3,
    "opening and closing chat inside one picker session must preserve its selection");
Require(!FarmingGridSelection.UpdatePickerSession(pickerOpen: false), "closing the picker must only end its session");
Require(FarmingGridSelection.CurrentSize == 3, "closing the picker must preserve the selected planting grid");
Require(FarmingGridSelection.UpdatePickerSession(pickerOpen: true), "reopening the picker must begin a new selection session");
Require(FarmingGridSelection.CurrentSize == 9, "each picker session must start at 9x9");
FarmingGridSelection.UpdatePickerSession(pickerOpen: false);

foreach (int size in new[] { 1, 3, 5, 7, 9 })
{
    Require(FarmingGridSelection.IsAllowed(size), $"{size}x{size} must be allowed");
    Require(
        FarmingGridSelection.ShouldHandleInput(
            cultivatorPickerOpen: true,
            leftShiftHeld: true,
            anotherModifierHeld: false,
            size),
        $"Left Shift+{size} must select while the Cultivator picker is open");
    Require(FarmingGridSelection.TrySelect(size), $"{size}x{size} must be selectable");
    Require(FarmingGridSelection.CurrentSize == size, $"{size}x{size} must become current");
}

foreach (int size in new[] { -1, 0, 2, 4, 6, 8, 10, 11 })
{
    int before = FarmingGridSelection.CurrentSize;
    Require(!FarmingGridSelection.IsAllowed(size), $"{size} must be rejected");
    Require(!FarmingGridSelection.TrySelect(size), $"{size} must not be selectable");
    Require(FarmingGridSelection.CurrentSize == before, "a rejected size must preserve the current selection");
}

foreach (int size in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })
{
    Require(
        !FarmingGridSelection.ShouldHandleInput(
            cultivatorPickerOpen: true,
            leftShiftHeld: false,
            anotherModifierHeld: false,
            size),
        $"plain {size} must remain native");
    Require(
        !FarmingGridSelection.ShouldHandleInput(
            cultivatorPickerOpen: false,
            leftShiftHeld: true,
            anotherModifierHeld: false,
            size),
        $"Left Shift+{size} must remain native while the picker is closed");
    Require(
        !FarmingGridSelection.ShouldHandleInput(
            cultivatorPickerOpen: true,
            leftShiftHeld: true,
            anotherModifierHeld: true,
            size),
        $"Left Shift plus another modifier and {size} must remain native");
}

foreach (int size in new[] { 2, 4, 6, 8 })
{
    Require(
        !FarmingGridSelection.ShouldHandleInput(
            cultivatorPickerOpen: true,
            leftShiftHeld: true,
            anotherModifierHeld: false,
            size),
        $"Left Shift+{size} must remain native");
}

FarmingGridSelection.Reset();
Require(FarmingGridSelection.CurrentSize == 9, "reset must restore the session default");

Console.WriteLine("farming grid selection tests passed");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
