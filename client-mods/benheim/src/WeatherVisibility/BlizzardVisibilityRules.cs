using System;

namespace BenheimQoL.WeatherVisibility;

internal static class BlizzardVisibilityRules
{
    internal const float TargetFogDensity = 0.01f;
    internal const float TargetSnowRate = 8f;

    internal static bool ShouldApply(
        bool enabled,
        string? environmentName,
        bool playerInMountain,
        bool environmentInMountain) =>
        enabled &&
        IsSnowStorm(environmentName) &&
        playerInMountain &&
        environmentInMountain;

    internal static bool IsSnowStorm(string? environmentName) =>
        string.Equals(environmentName, "SnowStorm", StringComparison.Ordinal) ||
        string.Equals(environmentName, "Twilight_SnowStorm", StringComparison.Ordinal);

    internal static bool ShouldRestoreStormTargets(
        string? environmentName,
        bool hasStormSnow) =>
        hasStormSnow && IsSnowStorm(environmentName);

    internal static float AdjustFogDensity(float nativeDensity) =>
        Math.Min(nativeDensity, TargetFogDensity);

    internal static bool IsSnowParticle(string? name) =>
        string.Equals(name, "Snow", StringComparison.Ordinal);

    internal static bool IsSuppressedStormParticle(string? name) =>
        string.Equals(name, "mist", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "smoke", StringComparison.OrdinalIgnoreCase);
}
