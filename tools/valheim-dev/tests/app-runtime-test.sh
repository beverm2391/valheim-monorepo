#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet run --project "$root/tests/app-runtime/ValheimDevAppTests.csproj"
rg -Fq 'appRuntime?.Update();' "$root/plugin/src/Plugin.cs"
rg -Fq 'appRuntime?.Dispose();' "$root/plugin/src/Plugin.cs"
