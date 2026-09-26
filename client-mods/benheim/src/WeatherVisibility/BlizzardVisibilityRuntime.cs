using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.WeatherVisibility;

internal static partial class BlizzardVisibilityRuntime
{
    private const string EnvironmentRootPath = "_GameMain/_Environment";
    private const string DistantFogPath = "Distant_fog_planes";
    private const string HeavyMistPath = "FollowPlayer/Mist/cloud (1)";

    private static readonly List<StormParticleState> stormParticles = new();
    private static readonly List<MistEmitterState> stormMistEmitters = new();
    private static readonly List<DistantFogState> distantFogEmitters = new();
    private static readonly List<GlobalHazeState> globalHazeParticles = new();

    private static GameObject[]? capturedParticleRoots;
    private static bool active;
    private static bool appliedReported;
    private static bool degradedReported;
    private static string operationId = string.Empty;
    private static float startedAt;

    internal static bool ShouldApply(EnvSetup? environment)
    {
        Player? player = Player.m_localPlayer;
        EnvMan? manager = EnvMan.instance;
        return environment != null && player != null && manager != null &&
            BlizzardVisibilityRules.ShouldApply(
                BlizzardVisibilitySettings.Enabled,
                environment.m_name,
                player.GetCurrentBiome() == Heightmap.Biome.Mountain,
                manager.GetCurrentBiome() == Heightmap.Biome.Mountain);
    }

    internal static EnvSetup CreateVisualEnvironment(EnvSetup environment)
    {
        EnvSetup adjusted = environment.Clone();
        adjusted.m_fogDensityNight = BlizzardVisibilityRules.AdjustFogDensity(
            environment.m_fogDensityNight);
        adjusted.m_fogDensityMorning = BlizzardVisibilityRules.AdjustFogDensity(
            environment.m_fogDensityMorning);
        adjusted.m_fogDensityDay = BlizzardVisibilityRules.AdjustFogDensity(
            environment.m_fogDensityDay);
        adjusted.m_fogDensityEvening = BlizzardVisibilityRules.AdjustFogDensity(
            environment.m_fogDensityEvening);
        return adjusted;
    }

    internal static void Update(EnvSetup? environment)
    {
        bool shouldApply = ShouldApply(environment);
        if (!shouldApply || environment == null)
        {
            if (active)
            {
                bool stormStillActive = environment != null &&
                    BlizzardVisibilityRules.ShouldRestoreStormTargets(
                        environment.m_name,
                        HasStormSnow(environment));
                Restore(stormStillActive, RestoreReason(environment));
            }
            return;
        }

        if (!CaptureStormTargets(environment))
        {
            if (active)
            {
                Restore(stormStillActive: false, "storm_particles_unavailable");
            }
            else
            {
                ClearState();
            }
            return;
        }

        if (!active)
        {
            active = true;
            appliedReported = false;
            degradedReported = false;
            operationId = Diagnostics.NewOperationId();
            startedAt = Time.time;
        }

        CaptureGlobalTargets();
        ApplyStormTargets();
        ApplyGlobalTargets();
        ReportApplied(environment);
    }

    internal static void Reset(string reason, bool restoreStormTargets = false)
    {
        if (active)
        {
            Restore(restoreStormTargets, reason);
            return;
        }

        ClearState();
    }

    private static bool HasStormSnow(EnvSetup environment)
    {
        foreach (GameObject root in environment.m_psystems ?? Array.Empty<GameObject>())
        {
            if (root == null)
            {
                continue;
            }

            foreach (ParticleSystem particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle != null && BlizzardVisibilityRules.IsSnowParticle(particle.name))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool CaptureStormTargets(EnvSetup environment)
    {
        GameObject[] roots = environment.m_psystems ?? Array.Empty<GameObject>();
        if (ReferenceEquals(capturedParticleRoots, roots) && HasCapturedSnow())
        {
            return true;
        }

        if (capturedParticleRoots != null && !ReferenceEquals(capturedParticleRoots, roots))
        {
            // EnvMan switches and disables the old array inside SetEnv before this
            // postfix runs. Forget its captured states so a later setting change
            // cannot re-enable roots that native weather has already retired.
            stormParticles.Clear();
            stormMistEmitters.Clear();
        }

        capturedParticleRoots = roots;
        bool hasSnow = false;
        foreach (GameObject root in roots)
        {
            if (root == null)
            {
                continue;
            }

            foreach (ParticleSystem particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle == null)
                {
                    continue;
                }

                bool snow = BlizzardVisibilityRules.IsSnowParticle(particle.name);
                bool suppressed = BlizzardVisibilityRules.IsSuppressedStormParticle(particle.name);
                hasSnow |= snow;
                if ((snow || suppressed) && !ContainsParticle(stormParticles, particle))
                {
                    stormParticles.Add(new StormParticleState(particle, snow, suppressed));
                }
            }

            foreach (MistEmitter emitter in root.GetComponentsInChildren<MistEmitter>(true))
            {
                if (emitter != null && !ContainsMistEmitter(emitter))
                {
                    stormMistEmitters.Add(new MistEmitterState(emitter));
                }
            }
        }
        return hasSnow;
    }

    private static bool HasCapturedSnow()
    {
        foreach (StormParticleState state in stormParticles)
        {
            if (state.Particle != null && state.IsSnow)
            {
                return true;
            }
        }
        return false;
    }

    private static void CaptureGlobalTargets()
    {
        distantFogEmitters.RemoveAll(state => state.Emitter == null);
        globalHazeParticles.RemoveAll(state => state.Particle == null);
        if (distantFogEmitters.Count > 0 && globalHazeParticles.Count > 0)
        {
            return;
        }

        GameObject? environmentRoot = GameObject.Find(EnvironmentRootPath);
        if (environmentRoot == null)
        {
            return;
        }

        Transform? distantFog = environmentRoot.transform.Find(DistantFogPath);
        DistantFogEmitter? emitter = distantFog != null
            ? distantFog.GetComponent<DistantFogEmitter>()
            : null;
        if (emitter != null && !ContainsDistantFog(emitter))
        {
            distantFogEmitters.Add(new DistantFogState(emitter));
        }

        Transform? heavyMist = environmentRoot.transform.Find(HeavyMistPath);
        ParticleSystem? particle = heavyMist != null
            ? heavyMist.GetComponent<ParticleSystem>()
            : null;
        ParticleSystemRenderer? renderer = particle != null
            ? particle.GetComponent<ParticleSystemRenderer>()
            : null;
        if (particle != null && renderer?.sharedMaterial != null &&
            string.Equals(renderer.sharedMaterial.name, "heavymist", StringComparison.Ordinal) &&
            !ContainsParticle(globalHazeParticles, particle))
        {
            globalHazeParticles.Add(new GlobalHazeState(particle));
        }
    }

    private static void ApplyStormTargets()
    {
        foreach (StormParticleState state in stormParticles)
        {
            if (state.Particle == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = state.Particle.emission;
            if (state.IsSnow)
            {
                emission.rateOverTimeMultiplier = BlizzardVisibilityRules.TargetSnowRate;
            }
            else if (state.IsSuppressed)
            {
                emission.enabled = false;
                state.Particle.Clear(true);
            }
        }

        foreach (MistEmitterState state in stormMistEmitters)
        {
            if (state.Emitter != null)
            {
                state.Emitter.enabled = false;
            }
        }
    }

    private static void ApplyGlobalTargets()
    {
        foreach (DistantFogState state in distantFogEmitters)
        {
            if (state.Emitter == null)
            {
                continue;
            }

            state.Emitter.enabled = false;
            foreach (ParticleSystem particle in state.Emitter.m_psystems ?? Array.Empty<ParticleSystem>())
            {
                particle?.Clear(true);
            }
        }

        foreach (GlobalHazeState state in globalHazeParticles)
        {
            if (state.Particle == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = state.Particle.emission;
            emission.enabled = false;
            state.Particle.Clear(true);
        }
    }

    private static void ReportApplied(EnvSetup environment)
    {
        if (appliedReported)
        {
            return;
        }

        appliedReported = true;
        Diagnostics.Emit(Event("storm_visibility_applied")
            .String("operation_phase", "start")
            .String("environment", environment.m_name)
            .Number("fog_density", BlizzardVisibilityRules.TargetFogDensity)
            .Number("snow_rate", BlizzardVisibilityRules.TargetSnowRate)
            .Integer("storm_particle_count", stormParticles.Count)
            .Integer("mist_emitter_count", stormMistEmitters.Count)
            .Integer("distant_fog_emitter_count", distantFogEmitters.Count)
            .Integer("global_haze_particle_count", globalHazeParticles.Count));

        if (!degradedReported &&
            (distantFogEmitters.Count == 0 || globalHazeParticles.Count == 0))
        {
            degradedReported = true;
            Diagnostics.Emit(Event("storm_visibility_degraded")
                .String("reason", "global_haze_target_missing")
                .Integer("distant_fog_emitter_count", distantFogEmitters.Count)
                .Integer("global_haze_particle_count", globalHazeParticles.Count));
        }
    }

    private static void Restore(bool stormStillActive, string reason)
    {
        foreach (StormParticleState state in stormParticles)
        {
            if (state.Particle == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = state.Particle.emission;
            emission.rateOverTimeMultiplier = state.OriginalRate;
            if (stormStillActive)
            {
                emission.enabled = state.OriginalEmission;
                if (state.OriginalPlaying)
                {
                    state.Particle.Play(true);
                }
            }
        }

        foreach (MistEmitterState state in stormMistEmitters)
        {
            if (stormStillActive && state.Emitter != null)
            {
                state.Emitter.enabled = state.OriginalEnabled;
            }
        }

        foreach (DistantFogState state in distantFogEmitters)
        {
            if (state.Emitter != null)
            {
                state.Emitter.enabled = state.OriginalEnabled;
            }
        }

        foreach (GlobalHazeState state in globalHazeParticles)
        {
            if (state.Particle == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = state.Particle.emission;
            emission.enabled = state.OriginalEmission;
            if (state.OriginalPlaying)
            {
                state.Particle.Play(true);
            }
        }

        Diagnostics.Emit(Event("storm_visibility_restored")
            .String("operation_phase", "terminal")
            .String("reason", reason)
            .Number("duration", Math.Max(0f, Time.time - startedAt)));
        ClearState();
    }

    private static string RestoreReason(EnvSetup? environment)
    {
        if (!BlizzardVisibilitySettings.Enabled)
        {
            return "setting_disabled";
        }
        if (environment == null || !BlizzardVisibilityRules.IsSnowStorm(environment.m_name))
        {
            return "weather_changed";
        }
        return "mountain_left";
    }

    private static DiagnosticEvent Event(string name) =>
        DiagnosticEvent.Create("WeatherVisibility", name)
            .String("operation_id", operationId);

    private static void ClearState()
    {
        stormParticles.Clear();
        stormMistEmitters.Clear();
        distantFogEmitters.Clear();
        globalHazeParticles.Clear();
        capturedParticleRoots = null;
        active = false;
        appliedReported = false;
        degradedReported = false;
        operationId = string.Empty;
        startedAt = 0f;
    }

    private static bool ContainsParticle<TState>(IEnumerable<TState> states, ParticleSystem particle)
        where TState : IParticleState
    {
        foreach (TState state in states)
        {
            if (state.Particle == particle)
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsMistEmitter(MistEmitter emitter)
    {
        foreach (MistEmitterState state in stormMistEmitters)
        {
            if (state.Emitter == emitter)
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsDistantFog(DistantFogEmitter emitter)
    {
        foreach (DistantFogState state in distantFogEmitters)
        {
            if (state.Emitter == emitter)
            {
                return true;
            }
        }
        return false;
    }

}
