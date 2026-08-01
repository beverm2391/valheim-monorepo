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

Expect(
    "Kept 1 protected stack",
    QuickStackMessages.ProtectedWorldText(1),
    "singular protected world text");
Expect(
    "Kept 3 protected stacks",
    QuickStackMessages.ProtectedWorldText(3),
    "plural protected world text");
Expect(
    "Kept 3 protected stacks (5 chests checked; 2 without a matching chest)",
    QuickStackMessages.NothingMoved(5, 3, 2, 0, 0),
    "protected no-move summary");

Console.WriteLine("quick-stack grouping and protected-message checks passed");
return 0;

static void Expect(string expectedValue, string actualValue, string scenario)
{
    if (!string.Equals(expectedValue, actualValue, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"{scenario}: expected '{expectedValue}', got '{actualValue}'");
    }
}
