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

grep -Fq "Packaged version: private-test \`$version\` for Mac and Windows." "$product_review"
grep -Fq 'Installed version: private-test `0.1.75` on Ben' "$product_review"
grep -Fq 'running.' "$product_review"
grep -Fq 'not installed' "$product_review"
grep -Fq 'has no packaged-build startup proof.' "$product_review"
grep -Fq '## Test on installed `0.1.75`' "$product_review"
grep -Fq '## After `0.1.76` installation' "$product_review"

installed_queue="$(sed -n '/^## Test on installed /,/^## After /p' "$product_review")"
if grep -Fq 'Earned-state audio' <<<"$installed_queue"; then
  echo "future-version audio proof must stay outside the installed-client queue" >&2
  exit 1
fi
grep -Fq '**Earned-state audio:**' "$product_review"

if grep -Eq 'SHA256SUMS\.txt|"\$release_dir/VERSION"|offers? to install future stable updates|releases/latest/download/VERSION' "$release_script"; then
  echo "release flow still publishes automatic-updater artifacts or instructions" >&2
  exit 1
fi

echo "release flow gates and manual distribution checks passed"
