using BepInEx.Configuration;

namespace BenheimQoL.CombatFeedback;

/// <summary>
/// Owns the four persisted client-local Benheim FX preferences. The master
/// preference overrides each family without changing its saved value.
/// </summary>
internal static class BenheimFxSettings
{
    private const string Section = "Benheim FX";

    private static ConfigEntry<bool>? master;
    private static ConfigEntry<bool>? bowFocus;
    private static ConfigEntry<bool>? combatShake;
    private static ConfigEntry<bool>? dangerArrival;

    internal static bool MasterEnabled => master?.Value ?? true;
    internal static bool BowFocusEnabled => MasterEnabled && (bowFocus?.Value ?? true);
    internal static bool CombatShakeEnabled => MasterEnabled && (combatShake?.Value ?? true);
    internal static bool DangerArrivalEnabled => MasterEnabled && (dangerArrival?.Value ?? true);

    internal static bool BowFocusPreference => bowFocus?.Value ?? true;
    internal static bool CombatShakePreference => combatShake?.Value ?? true;
    internal static bool DangerArrivalPreference => dangerArrival?.Value ?? true;

    internal static void Initialize(ConfigFile config)
    {
        master = config.Bind(
            Section,
            "Enabled",
            true,
            "Master switch for Bow Focus, Combat Shake, and Danger Arrival FX.");
        bowFocus = config.Bind(
            Section,
            "Bow Focus",
            true,
            "Smooth field-of-view focus while drawing a bow.");
        combatShake = config.Bind(
            Section,
            "Combat Shake",
            true,
            "Benheim headshot, Cleave, and mining AOE camera-shake requests.");
        dangerArrival = config.Bind(
            Section,
            "Danger Arrival FX",
            true,
            "Danger-arrival banner, stinger, and edge vignette.");
    }

    internal static void SetMaster(bool enabled)
    {
        Set(master, enabled);
    }

    internal static void SetBowFocus(bool enabled)
    {
        Set(bowFocus, enabled);
    }

    internal static void SetCombatShake(bool enabled)
    {
        Set(combatShake, enabled);
    }

    internal static void SetDangerArrival(bool enabled)
    {
        Set(dangerArrival, enabled);
    }

    private static void Set(ConfigEntry<bool>? entry, bool enabled)
    {
        if (entry != null)
        {
            entry.Value = enabled;
        }
    }
}
