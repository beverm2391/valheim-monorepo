using System;
using System.Collections.Generic;
using BenheimQoL.DeveloperDiagnostics;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Spawning;

/// <summary>
/// Samples only spawn rules explicitly registered by Benheim features. A hard
/// rule cap and fixed cadence keep this shipped-on evidence independent of the
/// number of native spawn rules or frames rendered.
/// </summary>
internal static partial class SpawnPopulationProbe
{
    internal const float SampleIntervalSeconds = 5f;
    internal const float HeartbeatIntervalSeconds = 60f;
    internal const int MaximumRegisteredRules = 32;

    private static readonly Dictionary<string, TrackedRule> Rules =
        new(StringComparer.Ordinal);

    private static bool registered;
    private static bool active;

    internal static void Register()
    {
        if (registered)
        {
            return;
        }

        DeveloperDiagnosticsRuntime.RegisterEventProbe(
            "spawns",
            shippedDefault: true,
            TrySetActive,
            Update,
            Cleanup);
        registered = true;
    }

    internal static void RegisterRule(
        string source,
        string prefab,
        SpawnSystem.SpawnData spawnData)
    {
        try
        {
            string key = source + "\n" + prefab;
            if (!Rules.TryGetValue(key, out TrackedRule? rule))
            {
                if (Rules.Count >= MaximumRegisteredRules)
                {
                    DeveloperDiagnosticsRuntime.ReportFailure(
                        "rule_registration",
                        "spawns",
                        $"registered rule limit {MaximumRegisteredRules} reached");
                    return;
                }

                rule = new TrackedRule(source, prefab, spawnData);
                Rules.Add(key, rule);
            }
            else
            {
                // One source/prefab identity is one telemetry stream even when
                // several loaded SpawnSystem instances expose equivalent
                // serialized rule objects. Retain the first live rule and
                // replace it only after Unity has released that object.
                if (rule.TryGetTarget(out SpawnSystem.SpawnData? current) &&
                    current != null)
                {
                    return;
                }
                rule.SetTarget(spawnData);
            }

            if (active)
            {
                Observe(rule, Time.realtimeSinceStartup, forceAvailable: true);
            }
        }
        catch (Exception exception)
        {
            DeveloperDiagnosticsRuntime.ReportFailure(
                "rule_registration",
                "spawns",
                exception.Message);
        }
    }

    internal static bool TrySetActive(bool requestedState, out string failure)
    {
        failure = string.Empty;
        if (active == requestedState)
        {
            return true;
        }

        active = requestedState;
        ResetObservations();
        if (active)
        {
            ObserveAll(Time.realtimeSinceStartup, forceAvailable: true);
        }
        return true;
    }

    internal static void Update()
    {
        if (!active)
        {
            return;
        }

        ObserveAll(Time.realtimeSinceStartup, forceAvailable: false);
    }

    internal static void Cleanup(DiagnosticProbeCleanupReason reason)
    {
        active = false;
        ResetObservations();
        if (reason == DiagnosticProbeCleanupReason.WorldExit ||
            reason == DiagnosticProbeCleanupReason.SessionReset)
        {
            Rules.Clear();
        }
        else
        {
            RemoveCollectedRules();
        }
    }

    private static void ObserveAll(float now, bool forceAvailable)
    {
        List<string>? collected = null;
        foreach (KeyValuePair<string, TrackedRule> entry in Rules)
        {
            TrackedRule rule = entry.Value;
            if (!rule.TryGetTarget(out _))
            {
                collected ??= new List<string>();
                collected.Add(entry.Key);
                continue;
            }
            if (rule.Faulted)
            {
                continue;
            }
            if (!forceAvailable && !SampleDue(rule, now))
            {
                continue;
            }

            try
            {
                Observe(rule, now, forceAvailable);
            }
            catch (Exception exception)
            {
                rule.Faulted = true;
                DeveloperDiagnosticsRuntime.ReportFailure(
                    "sample",
                    "spawns",
                    exception.Message);
            }
        }

        if (collected == null)
        {
            return;
        }
        for (int index = 0; index < collected.Count; index++)
        {
            Rules.Remove(collected[index]);
        }
    }

    private static void Observe(TrackedRule rule, float now, bool forceAvailable)
    {
        if (!rule.TryGetTarget(out SpawnSystem.SpawnData? spawnData) || spawnData == null)
        {
            return;
        }

        RuleConfiguration configuration = RuleConfiguration.Capture(
            rule.Source,
            rule.Prefab,
            spawnData);
        int loadedCount = SpawnSystem.GetNrOfInstances(
            spawnData.m_prefab,
            Vector3.zero,
            0f,
            eventCreaturesOnly: false,
            procreationOnly: false);
        bool saturated = configuration.ConfiguredCap > 0 &&
            loadedCount >= configuration.ConfiguredCap;
        bool configurationChanged = !rule.HasConfiguration ||
            !rule.Configuration.Equals(configuration);
        bool populationChanged = !rule.HasPopulation ||
            rule.LoadedCount != loadedCount;
        bool saturationChanged = rule.HasPopulation &&
            rule.Saturated != saturated;

        if (forceAvailable || configurationChanged)
        {
            EmitConfiguration(
                configuration,
                loadedCount,
                saturated,
                rule.HasConfiguration ? "changed" : "available");
        }

        bool heartbeatDue = rule.HasPopulation &&
            Elapsed(now, rule.LastPopulationEmissionAt) >= HeartbeatIntervalSeconds;
        if (forceAvailable || !rule.HasPopulation || populationChanged ||
            saturationChanged || heartbeatDue)
        {
            string reason;
            if (!rule.HasPopulation || forceAvailable)
            {
                reason = "available";
            }
            else if (saturationChanged)
            {
                reason = saturated
                    ? "configured_cap_entered"
                    : "configured_cap_exited";
            }
            else if (populationChanged)
            {
                reason = "count_changed";
            }
            else
            {
                reason = "heartbeat";
            }

            EmitPopulation(configuration, loadedCount, saturated, reason);
            rule.LastPopulationEmissionAt = now;
        }

        rule.Configuration = configuration;
        rule.HasConfiguration = true;
        rule.LoadedCount = loadedCount;
        rule.Saturated = saturated;
        rule.HasPopulation = true;
        rule.LastSampleAt = now;
    }

    private static bool SampleDue(TrackedRule rule, float now)
    {
        return !rule.HasSample ||
            now < rule.LastSampleAt ||
            Elapsed(now, rule.LastSampleAt) >= SampleIntervalSeconds;
    }

    private static float Elapsed(float now, float then)
    {
        return now >= then ? now - then : float.MaxValue;
    }

    private static void EmitConfiguration(
        RuleConfiguration configuration,
        int loadedCount,
        bool saturated,
        string reason)
    {
        DiagnosticEvent diagnosticEvent = DiagnosticEvent.Create(
            "Spawning",
            "spawn_probe_configuration");
        AddRuleFields(diagnosticEvent, configuration, loadedCount, saturated)
            .String("reason", reason);
        Diagnostics.Emit(diagnosticEvent);
    }

    private static void EmitPopulation(
        RuleConfiguration configuration,
        int loadedCount,
        bool saturated,
        string reason)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("Spawning", "spawn_probe_population")
                .String("source", configuration.Source)
                .String("prefab", configuration.Prefab)
                .Integer("loaded_count", loadedCount)
                .Integer("configured_loaded_population_cap", configuration.ConfiguredCap)
                .Boolean("configured_cap_saturated", saturated)
                .String("reason", reason));
    }

    private static DiagnosticEvent AddRuleFields(
        DiagnosticEvent diagnosticEvent,
        RuleConfiguration configuration,
        int loadedCount,
        bool saturated)
    {
        return diagnosticEvent
            .String("source", configuration.Source)
            .String("prefab", configuration.Prefab)
            .Number("spawn_interval_seconds", configuration.SpawnInterval)
            .Number("configured_spawn_chance_percent", configuration.ConfiguredSpawnChance)
            .Integer("configured_loaded_population_cap", configuration.ConfiguredCap)
            .Integer("group_size_min", configuration.GroupSizeMin)
            .Integer("group_size_max", configuration.GroupSizeMax)
            .Number("same_prefab_spacing_meters", configuration.SpawnDistance)
            .String("biome", configuration.Biome)
            .String("biome_area", configuration.BiomeArea)
            .Number("min_altitude", configuration.MinAltitude)
            .Number("max_altitude", configuration.MaxAltitude)
            .Integer("loaded_count", loadedCount)
            .Boolean("configured_cap_saturated", saturated);
    }

    private static void ResetObservations()
    {
        foreach (TrackedRule rule in Rules.Values)
        {
            rule.ResetObservation();
        }
    }

    private static void RemoveCollectedRules()
    {
        List<string>? collected = null;
        foreach (KeyValuePair<string, TrackedRule> entry in Rules)
        {
            if (entry.Value.TryGetTarget(out _))
            {
                continue;
            }
            collected ??= new List<string>();
            collected.Add(entry.Key);
        }
        if (collected == null)
        {
            return;
        }
        for (int index = 0; index < collected.Count; index++)
        {
            Rules.Remove(collected[index]);
        }
    }

}
