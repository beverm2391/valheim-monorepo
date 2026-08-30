#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
release_script="$root/scripts/release.sh"
prompt="$root/../../PROMPT.md"
product_review="$root/../../PRODUCT_REVIEW.md"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"

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

# The integration lead owns release-state freshness and unproven queue entries,
# while Ben and the Project Lead own acceptance judgments.
grep -Fq '`PRODUCT_REVIEW.md` is the live release ledger and acceptance queue.' "$prompt"
grep -Fq 'integration lead for each client release must record its exact packaged' "$prompt"
grep -Fq 'exact installed version, and concise remaining live checks.' "$prompt"
grep -Fq 'The integration lead may add unproven items.' "$prompt"
grep -Fq 'Ben and the Project Lead own acceptance judgments.' "$prompt"
grep -Fq 'mark behavior from its own release as accepted;' "$prompt"
grep -Fq 'remove passed items based only on static proof; or' "$prompt"
grep -Fq 'promote behavior into accepted `PRODUCT.md` truth.' "$prompt"
grep -Fq 'the integration lead removes it from the queue and' "$prompt"

release_state="$(awk '
  /^## Release state$/ { capture = 1; next }
  capture && /^## / { exit }
  capture { print }
' "$product_review")"
grep -Fq "Packaged version: private-test \`$version\` for Mac and Windows." <<<"$release_state"
grep -Fq "Installed version: private-test \`$version\` on Ben's Mac, installed from the" <<<"$release_state"
grep -Fq 'Startup proof: The managed Benheim launcher started the exact installed macOS' <<<"$release_state"
grep -Fq 'expected version, session-start, and chainloader-complete' <<<"$release_state"
grep -Fq 'No world was entered.' <<<"$release_state"
release_state_flat="$(awk '{$1 = $1; printf "%s ", $0}' <<<"$release_state")"
grep -Fq 'The task quit only the Valheim process that it launched, and no Valheim process remained.' <<<"$release_state_flat"
! grep -Fq 'not installed and has no packaged-build startup proof.' <<<"$release_state"

installed_heading="## Test on installed \`$version\`"
installed_queue="$(awk -v heading="$installed_heading" '
  $0 == heading { capture = 1 }
  capture && /^## / && $0 != heading { exit }
  capture { print }
' "$product_review")"
grep -Fxq "$installed_heading" <<<"$installed_queue"
grep -Fq '**Earned-state audio:**' <<<"$installed_queue"

if grep -Eq 'SHA256SUMS\.txt|"\$release_dir/VERSION"|offers? to install future stable updates|releases/latest/download/VERSION' "$release_script"; then
  echo "release flow still publishes automatic-updater artifacts or instructions" >&2
  exit 1
fi

echo "release flow gates and manual distribution checks passed"
