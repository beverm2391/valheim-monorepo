using BepInEx.Configuration;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.WeatherVisibility;

internal static class BlizzardVisibilitySettings
{
    private const string Section = "Weather Visibility";
    private static ConfigEntry<bool>? enabled;

    internal static bool Enabled => enabled?.Value ?? true;

    internal static void Initialize(ConfigFile config)
    {
        enabled = config.Bind(
            Section,
            "Blizzard Visibility",
            true,
            "Reduce Mountain snowstorm snow, fog, and opaque mist while preserving native weather and gameplay.");
    }

    internal static void SetEnabled(bool value)
    {
        if (enabled == null || enabled.Value == value)
        {
            return;
        }

        enabled.Value = value;
        Diagnostics.Emit(DiagnosticEvent.Create("WeatherVisibility", "setting_changed")
            .Boolean("enabled", value));
    }
}
