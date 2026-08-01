using System;
using BenheimQoL.InventoryFeature;

var summary = new QuickStackSummary();
summary.Add(101, "Chest", "Resin", 3);
summary.Add(202, "Black metal chest", "Needle", 2);
summary.Add(101, "Chest", "Wood", 10);
summary.Add(101, "Chest", "Resin", 4);
summary.Add(202, "Black metal chest", "Dandelion", 2);

const string expected =
    "Chest 1: 10x Wood, 7x Resin\n" +
    "Black metal chest 2: 2x Dandelion, 2x Needle";
string actual = summary.Format();
if (!string.Equals(expected, actual, StringComparison.Ordinal))
{
    Console.Error.WriteLine("Expected:");
    Console.Error.WriteLine(expected);
    Console.Error.WriteLine("Actual:");
    Console.Error.WriteLine(actual);
    return 1;
}

Console.WriteLine("quick-stack summary grouping checks passed");
return 0;
