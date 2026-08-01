using System;
using BenheimEternalFire;

static void Expect(bool expected, float currentFuel, float maxFuel, string scenario)
{
    bool actual = RefillPolicy.ShouldRefill(currentFuel, maxFuel);
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"{scenario}: expected {expected}, got {actual} for current={currentFuel}, max={maxFuel}");
    }
}

Expect(true, -1f, 4f, "missing native fuel field initializes");
Expect(true, 0f, 4f, "empty piece initializes");
Expect(true, 0.999f, 4f, "below threshold refills");
Expect(true, 1f, 4f, "threshold is inclusive");
Expect(false, 1.001f, 4f, "above threshold waits");
Expect(false, 3f, 4f, "ordinary client fuel update is ignored");
Expect(false, 4f, 4f, "full piece is ignored");
Expect(false, 5f, 4f, "overfilled piece is ignored");
Expect(true, 0.49f, 0.5f, "small-capacity piece refills below max");
Expect(false, 0.5f, 0.5f, "small-capacity full piece is ignored");
Expect(false, 0f, 0f, "invalid max fuel is ignored");
Expect(false, float.NaN, 4f, "NaN current fuel is ignored");
Expect(false, 0f, float.PositiveInfinity, "infinite max fuel is ignored");

Console.WriteLine("PASS: refill policy decision boundary");
