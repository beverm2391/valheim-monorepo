#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
release_script="$root/scripts/release.sh"
prompt="$root/../../PROMPT.md"

grep -Fq 'git status --porcelain' "$release_script"
grep -Fq 'git branch --show-current' "$release_script"
grep -Fq 'Local $release_branch must exactly match origin/$release_branch.' "$release_script"
grep -Fq '"$root/scripts/package-all.sh"' "$release_script"
grep -Fq 'Benheim-macOS.zip' "$release_script"
grep -Fq 'Benheim-Windows.zip' "$release_script"
grep -Fq 'gh release create "$tag"' "$release_script"
grep -Fq 'Rerun the installer' "$release_script"
grep -Fq 'normal Steam Play button starts vanilla Valheim' "$release_script"

# A packaged-build startup gate cannot clear or adopt a Valheim process that
# predates the task running the gate. Only that task's own bounded proof process
# may be closed after validation.
grep -Fq 'Before this task installs or launches a packaged build for bounded startup' "$prompt"
grep -Fq 'already running is a hard stop.' "$prompt"
grep -Fq 'Do not quit or kill it, install over it, or' "$prompt"
grep -Fq 'launch or relaunch around it.' "$prompt"
grep -Fq "Wait for Ben's explicit instruction." "$prompt"
grep -Fq 'task may quit only the Valheim process that it launched for this bounded' "$prompt"
grep -Fq 'startup proof.' "$prompt"

if grep -Eq 'SHA256SUMS\.txt|"\$release_dir/VERSION"|offers? to install future stable updates|releases/latest/download/VERSION' "$release_script"; then
  echo "release flow still publishes automatic-updater artifacts or instructions" >&2
  exit 1
fi

echo "release flow gates and manual distribution checks passed"
