using System;
using BenheimQoL.WeatherVisibility;

Expect(BlizzardVisibilityCaptureLimits.WrittenRoots(20) == 16,
    "root output is capped");
Expect(BlizzardVisibilityCaptureLimits.WrittenRoots(-1) == 0,
    "negative root counts cannot produce output");

int written = 0;
written += BlizzardVisibilityCaptureLimits.WrittenNodes(200, written);
Expect(written == 96, "one root cannot monopolize the global node budget");
written += BlizzardVisibilityCaptureLimits.WrittenNodes(200, written);
Expect(written == 192, "a second root retains its per-root budget");
written += BlizzardVisibilityCaptureLimits.WrittenNodes(200, written);
Expect(written == 256, "the final root receives only the remaining global budget");
written += BlizzardVisibilityCaptureLimits.WrittenNodes(200, written);
Expect(written == 256, "later roots cannot exceed the global node budget");

Expect(BlizzardVisibilityCaptureLimits.WrittenComponents(30) == 16,
    "component names are capped per node");
Expect(BlizzardVisibilityCaptureLimits.WrittenMaterials(9) == 4,
    "material identities are capped per renderer");

Console.WriteLine("weather visibility capture limits passed");

static void Expect(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Expectation failed: {description}");
    }
}
