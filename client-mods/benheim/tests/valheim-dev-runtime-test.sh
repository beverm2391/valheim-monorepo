#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tests="$root/tests/valheim-dev-runtime"

dotnet build "$tests/fixtures/Good/Good.csproj" --configuration Release --nologo
dotnet build "$tests/fixtures/NoCleanup/NoCleanup.csproj" --configuration Release --nologo
dotnet build "$tests/fixtures/Throwing/Throwing.csproj" --configuration Release --nologo
dotnet build "$tests/fixtures/BadEntrypoint/BadEntrypoint.csproj" --configuration Release --nologo
dotnet build "$tests/fixtures/FailCleanup/FailCleanup.csproj" --configuration Release --nologo

dotnet run --project "$tests/ValheimDevRuntimeTests.csproj" -- \
  "$tests/fixtures/Good/bin/Release/net8.0/ValheimDevGood.dll" \
  "$tests/fixtures/NoCleanup/bin/Release/net8.0/ValheimDevNoCleanup.dll" \
  "$tests/fixtures/Throwing/bin/Release/net8.0/ValheimDevThrowing.dll" \
  "$tests/fixtures/BadEntrypoint/bin/Release/net8.0/ValheimDevBadEntrypoint.dll" \
  "$tests/fixtures/FailCleanup/bin/Release/net8.0/ValheimDevFailCleanup.dll"

rg -Fq 'ValheimDevRuntime.TryHandleConsole(args.Args, args.Context)' "$root/src/EnemyTiers/BenheimTestCommandClient.cs"
rg -Fq 'ValheimDevRuntime.Update();' "$root/src/Plugin.cs"
rg -Fq 'ValheimDevRuntime.Revoke("plugin_teardown");' "$root/src/Plugin.cs"
rg -Fq '[HarmonyPatch(typeof(ZNet), "OnDestroy")]' "$root/src/ValheimDev/ValheimDevRuntime.cs"
rg -Fq '.Boolean("grounded_after_physics"' "$root/src/Affinities/LungeRuntime.cs"
rg -Fq '.Boolean("swimming_after_physics"' "$root/src/Affinities/LungeRuntime.cs"
rg -Fq '.Boolean("flying_after_physics"' "$root/src/Affinities/LungeRuntime.cs"
rg -Fq '.Boolean("attached_after_physics"' "$root/src/Affinities/LungeRuntime.cs"

printf 'Valheim Dev runtime integration checks passed\n'
