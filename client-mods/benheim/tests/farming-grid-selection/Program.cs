using System;
using BenheimQoL.Farming;

FarmingGridSelection.Reset();
Require(FarmingGridSelection.CurrentSize == 9, "each session must default to 9x9");

foreach (int size in new[] { 1, 3, 5, 7, 9 })
{
    Require(FarmingGridSelection.IsAllowed(size), $"{size}x{size} must be allowed");
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
