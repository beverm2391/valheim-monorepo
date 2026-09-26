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

Expect(BlizzardVisibilityRules.ShouldApply(
        enabled: true,
        environmentName: "SnowStorm",
        playerInMountain: true,
        environmentInMountain: true),
    "Mountain SnowStorm is eligible when the setting is on");
Expect(BlizzardVisibilityRules.ShouldApply(
        enabled: true,
        environmentName: "Twilight_SnowStorm",
        playerInMountain: true,
        environmentInMountain: true),
    "Mountain Twilight SnowStorm is eligible when the setting is on");
Expect(!BlizzardVisibilityRules.ShouldApply(
        enabled: false,
        environmentName: "SnowStorm",
        playerInMountain: true,
        environmentInMountain: true),
    "the player setting disables the visual change");
Expect(!BlizzardVisibilityRules.ShouldApply(
        enabled: true,
        environmentName: "SnowStorm",
        playerInMountain: false,
        environmentInMountain: true),
    "the player must be in the Mountain");
Expect(!BlizzardVisibilityRules.ShouldApply(
        enabled: true,
        environmentName: "SnowStorm",
        playerInMountain: true,
        environmentInMountain: false),
    "the environment must be in the Mountain");
Expect(!BlizzardVisibilityRules.ShouldApply(
        enabled: true,
        environmentName: "Clear",
        playerInMountain: true,
        environmentInMountain: true),
    "clear Mountain weather stays native");
Expect(BlizzardVisibilityRules.ShouldRestoreStormTargets(
        environmentName: "SnowStorm",
        hasStormSnow: true),
    "turning the setting off during the live storm restores captured targets");
Expect(BlizzardVisibilityRules.ShouldRestoreStormTargets(
        environmentName: "Twilight_SnowStorm",
        hasStormSnow: true),
    "leaving the Mountain during a live twilight storm restores captured targets");
Expect(!BlizzardVisibilityRules.ShouldRestoreStormTargets(
        environmentName: "Clear",
        hasStormSnow: false),
    "weather teardown leaves old storm roots to the native environment switch");
Expect(!BlizzardVisibilityRules.ShouldRestoreStormTargets(
        environmentName: "Clear",
        hasStormSnow: true),
    "a clear environment name never revives lingering snowstorm roots");

Expect(Math.Abs(BlizzardVisibilityRules.AdjustFogDensity(0.05f) - 0.01f) < 0.0001f,
    "native SnowStorm fog is capped at the approved target");
Expect(Math.Abs(BlizzardVisibilityRules.AdjustFogDensity(0.005f) - 0.005f) < 0.0001f,
    "the visibility rule never adds fog during a transition");
Expect(BlizzardVisibilityRules.IsSnowParticle("Snow"),
    "the native Snow particle keeps a reduced emission rate");
Expect(BlizzardVisibilityRules.IsSuppressedStormParticle("mist") &&
        BlizzardVisibilityRules.IsSuppressedStormParticle("smoke"),
    "the two proven opaque storm layers are suppressed");
Expect(!BlizzardVisibilityRules.IsSuppressedStormParticle("embers"),
    "unknown particle layers remain native");

Console.WriteLine("weather visibility rules and capture limits passed");

static void Expect(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Expectation failed: {description}");
    }
}
