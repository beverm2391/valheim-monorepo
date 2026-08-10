#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet run --project "$root/tests/top-left-feedback-layout/TopLeftFeedbackLayoutTests.csproj"
