#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
release_script="$root/scripts/release.sh"

grep -Fq 'git status --porcelain' "$release_script"
grep -Fq 'git branch --show-current' "$release_script"
grep -Fq 'Local $release_branch must exactly match origin/$release_branch.' "$release_script"
grep -Fq '"$root/scripts/package-all.sh"' "$release_script"
grep -Fq 'Benheim-macOS.zip' "$release_script"
grep -Fq 'Benheim-Windows.zip' "$release_script"
grep -Fq 'gh release create "$tag"' "$release_script"
grep -Fq 'Rerun the installer' "$release_script"
grep -Fq 'normal Steam Play button starts vanilla Valheim' "$release_script"

if grep -Eq 'SHA256SUMS\.txt|"\$release_dir/VERSION"|offers? to install future stable updates|releases/latest/download/VERSION' "$release_script"; then
  echo "release flow still publishes automatic-updater artifacts or instructions" >&2
  exit 1
fi

echo "release flow gates and manual distribution checks passed"
