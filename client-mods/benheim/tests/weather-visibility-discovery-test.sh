#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
discovery="$root/src/WeatherVisibility/BlizzardVisibilityDiscovery.cs"
rules="$root/src/WeatherVisibility/BlizzardVisibilityRules.cs"
runtime="$root/src/WeatherVisibility/BlizzardVisibilityRuntime.cs"
patches="$root/src/WeatherVisibility/BlizzardVisibilityPatches.cs"
settings="$root/src/WeatherVisibility/BlizzardVisibilitySettings.cs"
config_ui="$root/src/Shortcuts/ShortcutOverlayWeatherConfig.cs"
plugin="$root/src/Plugin.cs"
registry="$root/src/DeveloperDiagnostics/DeveloperDiagnosticsRuntime.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_env="$source_tree/EnvMan.cs"
native_version="$source_tree/Version.cs"

# Installed Valheim 1.0 owns weather through these runtime objects. The probe
# must discover their loaded children instead of encoding an old mod's paths.
grep -Fq 'public static GameVersion CurrentVersion { get; } = new GameVersion(1, 0, 16);' "$native_version"
grep -Fq 'private void SetParticleArrayEnabled(GameObject[] psystems, bool enabled)' "$native_env"
grep -Fq 'if (env.m_envObject != m_currentEnvObject)' "$native_env"
grep -Fq 'RenderSettings.fogDensity += env.m_fogDensityDay * dayInt;' "$native_env"
grep -Fq 'public EnvSetup GetCurrentEnvironment()' "$native_env"

grep -Fq '["blizzard"] = BlizzardVisibilityDiscovery.Run' "$registry"
grep -Fq 'environmentManager?.GetCurrentEnvironment()' "$discovery"
grep -Fq 'player.GetCurrentBiome() != Heightmap.Biome.Mountain' "$discovery"
grep -Fq 'environmentBiome != Heightmap.Biome.Mountain' "$discovery"
grep -Fq 'root.GetComponentsInChildren<Transform>(includeInactive: true)' "$discovery"
grep -Fq 'renderer.sharedMaterials' "$discovery"
grep -Fq 'for (int index = 0; index < writtenParticleRoots; index++)' "$discovery"
grep -Fq 'for (int nodeIndex = 0; nodeIndex < writtenNodes; nodeIndex++)' "$discovery"
grep -Fq '.Boolean("mutation_attempted", false)' "$discovery"
grep -Fq 'DiagnosticEvent.Create("WeatherVisibility", name)' "$discovery"

grep -Fq 'internal const float TargetFogDensity = 0.01f;' "$rules"
grep -Fq 'internal const float TargetSnowRate = 8f;' "$rules"
grep -Fq '"SnowStorm"' "$rules"
grep -Fq '"Twilight_SnowStorm"' "$rules"
grep -Fq 'player.GetCurrentBiome() == Heightmap.Biome.Mountain' "$runtime"
grep -Fq 'manager.GetCurrentBiome() == Heightmap.Biome.Mountain' "$runtime"
grep -Fq 'EnvSetup adjusted = environment.Clone();' "$runtime"
grep -Fq 'BlizzardVisibilityRules.ShouldRestoreStormTargets(' "$runtime"
grep -Fq 'ReferenceEquals(capturedParticleRoots, roots)' "$runtime"
grep -Fq 'cannot re-enable roots that native weather has already retired' "$runtime"
grep -Fq 'emission.rateOverTimeMultiplier = BlizzardVisibilityRules.TargetSnowRate;' "$runtime"
grep -Fq 'state.Emitter.enabled = false;' "$runtime"
grep -Fq 'state.Emitter.enabled = state.OriginalEnabled;' "$runtime"
grep -Fq '"_GameMain/_Environment"' "$runtime"
grep -Fq '"Distant_fog_planes"' "$runtime"
grep -Fq '"FollowPlayer/Mist/cloud (1)"' "$runtime"
grep -Fq '"heavymist"' "$runtime"
grep -Fq '"storm_visibility_applied"' "$runtime"
grep -Fq '"storm_visibility_restored"' "$runtime"
grep -Fq '[HarmonyPatch(typeof(EnvMan), "SetEnv")]' "$patches"
grep -Fq 'env = BlizzardVisibilityRuntime.CreateVisualEnvironment(env);' "$patches"
grep -Fq 'config.Bind(' "$settings"
grep -Fq '"Blizzard Visibility"' "$settings"
grep -Fq 'BuildWeatherVisibilityConfig' "$root/src/Shortcuts/ShortcutOverlayConfig.cs"
grep -Fq 'BlizzardVisibilitySettings.SetEnabled' "$config_ui"
grep -Fq 'BlizzardVisibilitySettings.Initialize(Config);' "$plugin"
grep -Fq 'BlizzardVisibilityRuntime.Reset("plugin_teardown", restoreStormTargets: true);' "$plugin"

for forbidden in AddStatusEffect RemoveStatusEffect m_debugEnv m_isCold m_isFreezing; do
  if rg -Fq "$forbidden" "$runtime" "$patches" "$settings"; then
    printf 'Blizzard Visibility must not change weather selection or gameplay consequences\n' >&2
    exit 1
  fi
done

if rg -n 'SetActive\(|\.enabled\s*=|\.material(s)?\b' "$discovery"; then
  printf 'the Blizzard Visibility discovery snapshot must remain observation-only\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/weather-visibility-capture/WeatherVisibilityCaptureTests.csproj"

printf 'weather visibility source and behavior checks passed\n'
