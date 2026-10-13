#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
safe dotnet run --project "$root/tests/resident-speech-trace/ResidentSpeechTracePacketTests.csproj"
