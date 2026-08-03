#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
release_script="$root/scripts/release.sh"

grep -Fq 'git status --porcelain' "$release_script"
grep -Fq 'git branch --show-current' "$release_script"
grep -Fq 'Local $release_branch must exactly match origin/$release_branch.' "$release_script"
grep -Fq 'for test_script in "$root"/tests/*-test.sh' "$release_script"
grep -Fq 'QuickStackSummaryTests.csproj' "$release_script"
grep -Fq 'package-macos.sh' "$release_script"
grep -Fq 'package-windows.sh' "$release_script"
grep -Fq 'Benheim-macOS.zip' "$release_script"
grep -Fq 'Benheim-Windows.zip' "$release_script"
grep -Fq 'SHA256SUMS.txt' "$release_script"
grep -Fq 'gh release create "$tag"' "$release_script"
grep -Fq 'releases/latest/download/Benheim-macOS.zip' "$release_script"
grep -Fq 'releases/latest/download/Benheim-Windows.zip' "$release_script"
grep -Fq 'open \`Update Benheim\`' "$release_script"

echo "release flow gates and stable asset checks passed"
