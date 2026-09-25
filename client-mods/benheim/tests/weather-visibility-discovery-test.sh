#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
discovery="$root/src/WeatherVisibility/BlizzardVisibilityDiscovery.cs"
registry="$root/src/DeveloperDiagnostics/DeveloperDiagnosticsRuntime.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_env="$source_tree/EnvMan.cs"
native_version="$source_tree/Version.cs"

# Installed Valheim 1.0 owns weather through these runtime objects. The probe
# must discover their loaded children instead of encoding an old mod's paths.
grep -Fq 'public static GameVersion CurrentVersion { get; } = new GameVersion(1, 0, 15);' "$native_version"
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

if rg -n 'SetActive\(|\.enabled\s*=|\.material(s)?\b' "$discovery"; then
  printf 'the Blizzard Visibility discovery snapshot must remain observation-only\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/weather-visibility-capture/WeatherVisibilityCaptureTests.csproj"

printf 'weather visibility discovery source checks passed\n'
