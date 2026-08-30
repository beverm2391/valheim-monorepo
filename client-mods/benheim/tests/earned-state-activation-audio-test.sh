#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet run \
  --project "$root/tests/earned-state-activation-audio/EarnedStateActivationAudioTests.csproj" \
  -c Release
