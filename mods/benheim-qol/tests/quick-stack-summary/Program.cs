using System;
using BenheimQoL.InventoryFeature;

var summary = new QuickStackSummary();
summary.Add(101, "Chest", "4m NE", "Resin", 3);
summary.Add(202, "Black metal chest", "11m S", "Needle", 2);
summary.Add(101, "Chest", "4m NE", "Wood", 10);
summary.Add(101, "Chest", "4m NE", "Resin", 4);
summary.Add(202, "Black metal chest", "11m S", "Dandelion", 2);

const string expected =
    "Chest 1 (4m NE): 10x Wood, 7x Resin\n" +
    "Black metal chest 2 (11m S): 2x Dandelion, 2x Needle";
string actual = summary.Format();
if (!string.Equals(expected, actual, StringComparison.Ordinal))
{
    Console.Error.WriteLine("Expected:");
    Console.Error.WriteLine(expected);
    Console.Error.WriteLine("Actual:");
    Console.Error.WriteLine(actual);
    return 1;
}

Expect(
    "10x Wood, 7x Resin",
    summary.FormatItemsForContainer(101),
    "destination marker summary");
Expect(
    string.Empty,
    summary.FormatItemsForContainer(999),
    "destination marker for unused chest");

Expect(
    "Nothing moved (5 chests; 2 without a matching chest)",
    QuickStackMessages.NothingMoved(5, 2, 0, 0),
    "no-move summary");
Expect(
    "Nothing to put away",
    QuickStackMessages.AbovePlayerSummary(0),
    "closed-inventory empty summary");
Expect(
    "Put away 1 item",
    QuickStackMessages.AbovePlayerSummary(1),
    "closed-inventory singular summary");
Expect(
    "Put away 17 items",
    QuickStackMessages.AbovePlayerSummary(17),
    "closed-inventory plural summary");

Console.WriteLine("quick-stack grouping and presentation checks passed");
return 0;

static void Expect(string expectedValue, string actualValue, string scenario)
{
    if (!string.Equals(expectedValue, actualValue, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"{scenario}: expected '{expectedValue}', got '{actualValue}'");
    }
}
