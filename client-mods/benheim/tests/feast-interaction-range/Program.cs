using System;
using BenheimQoL.Interaction;

ExpectEqual(8f, FeastInteractionRange.Resolve(2f), "native Feast range expands to Benheim use distance");
ExpectEqual(8f, FeastInteractionRange.Resolve(8f), "matching Feast range stays unchanged");
ExpectEqual(12f, FeastInteractionRange.Resolve(12f), "larger Feast range stays unchanged");

Console.WriteLine("Feast interaction range behavior checks passed");

static void ExpectEqual(float expected, float actual, string scenario)
{
    if (expected != actual)
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}
