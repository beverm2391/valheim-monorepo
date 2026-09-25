using System;
using BenheimQoL.Crafting;

ExpectEqual(
    0,
    CraftingBatchRules.FindMaximumCrafts(0, _ => true),
    "a full inventory allows no craft");
ExpectEqual(
    0,
    CraftingBatchRules.FindMaximumCrafts(20, _ => false),
    "missing requirements allow no craft");
ExpectEqual(
    7,
    CraftingBatchRules.FindMaximumCrafts(20, count => count <= 7),
    "materials can be the limiting rule");
ExpectEqual(
    4,
    CraftingBatchRules.FindMaximumCrafts(4, count => count <= 20),
    "inventory capacity can be the limiting rule");
ExpectEqual(
    31,
    CraftingBatchRules.FindMaximumCrafts(31, _ => true),
    "the exact capacity is returned when every count is allowed");
ExpectEqual(
    1,
    CraftingBatchRules.FindMaximumCrafts(1, _ => true),
    "one affordable craft remains available");
ExpectEqual(
    8,
    CraftingBatchRules.FindMaximumCraftsDescending(
        10,
        count => count == 8 || count <= 3),
    "one-ingredient search handles a non-monotonic allowed range");
ExpectEqual(
    0,
    CraftingBatchRules.FindMaximumCraftsDescending(10, _ => false),
    "one-ingredient search returns zero when no quantity is allowed");

int calls = 0;
int maximum = CraftingBatchRules.FindMaximumCrafts(
    4096,
    count =>
    {
        calls++;
        return count <= 1537;
    });
ExpectEqual(1537, maximum, "large batches find the exact boundary");
if (calls > 14)
{
    throw new InvalidOperationException(
        $"maximum search should remain logarithmic: observed {calls} checks");
}

Console.WriteLine("crafting batch rule checks passed");
return;

static void ExpectEqual(int expected, int actual, string scenario)
{
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"{scenario}: expected {expected}, got {actual}");
    }
}
