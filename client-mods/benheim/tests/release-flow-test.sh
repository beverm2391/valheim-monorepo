#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
release_script="$root/scripts/release.sh"
prompt="$root/PROMPT.md"
repo_prompt="$root/../../PROMPT.md"
product_review="$root/../../PRODUCT_REVIEW.md"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
installed_version="0.1.80"

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
prompt_flat="$(awk '{$1 = $1; printf "%s ", $0}' "$prompt")"
grep -Fq 'Before this task installs or launches a packaged build for bounded startup' <<<"$prompt_flat"
grep -Fq 'already running is a hard stop.' <<<"$prompt_flat"
grep -Fq 'Do not quit or kill it, install over it, or launch or relaunch around it.' <<<"$prompt_flat"
grep -Fq "Wait for Ben's explicit instruction." <<<"$prompt_flat"
grep -Fq 'task may quit only the Valheim process that it launched for this bounded startup proof.' <<<"$prompt_flat"

# Ben and the Project Lead own product documents. Integration leads own only
# release-state freshness and unproven Product Review queue entries.
repo_prompt_flat="$(awk '{$1 = $1; printf "%s ", $0}' "$repo_prompt")"
grep -Fq 'During product discussion, Ben and the Project Lead create the feature folder and its owning `PRODUCT.md` before they dispatch feasibility or implementation work.' <<<"$repo_prompt_flat"
grep -Fq 'Dev Leads and agents working on integration tasks do not edit `PRODUCT.md` files.' <<<"$repo_prompt_flat"
grep -Fq 'They report feasibility and implementation evidence to the Project Lead' <<<"$repo_prompt_flat"
grep -Fq '`../../PRODUCT_REVIEW.md` is the live release ledger and acceptance queue.' <<<"$prompt_flat"
grep -Fq 'integration lead for each client release records the exact packaged version, exact installed version, and concise remaining live checks.' <<<"$prompt_flat"
grep -Fq 'The integration lead may add unproven items.' <<<"$prompt_flat"
grep -Fq 'Ben and the Project Lead own acceptance judgments.' <<<"$prompt_flat"
grep -Fq 'mark behavior from its own release as accepted;' <<<"$prompt_flat"
grep -Fq 'remove passed items based only on static proof; or' <<<"$prompt_flat"
grep -Fq 'promote behavior into accepted `PRODUCT.md` truth.' <<<"$prompt_flat"
grep -Fq 'the integration lead removes it from the queue. The Project Lead updates the owning product document.' <<<"$prompt_flat"

release_state="$(awk '
  /^## Release state$/ { capture = 1; next }
  capture && /^## / { exit }
  capture { print }
' "$product_review")"
release_state_flat="$(awk '{$1 = $1; printf "%s ", $0}' <<<"$release_state")"
if grep -Fq "Candidate version: private-test \`$version\` for Mac and Windows." <<<"$release_state"; then
  grep -Fq "not packaged or installed and has no packaged-build startup proof." <<<"$release_state_flat"
else
  grep -Fq "Packaged version: private-test \`$version\` for Mac and Windows." <<<"$release_state"
  if [[ "$version" == "$installed_version" ]]; then
    grep -Fq "The Windows package remains packaged-only." <<<"$release_state_flat"
  else
    grep -Fq "not installed and has no packaged-build startup proof." <<<"$release_state_flat"
  fi
fi
grep -Fq "Installed version: private-test \`$installed_version\` on Ben's Mac, installed from the" <<<"$release_state"
grep -Fq 'Startup proof: The managed Benheim launcher started the exact installed' <<<"$release_state"
grep -Fq 'expected version, session-start,' <<<"$release_state"
grep -Fq 'chainloader-complete, and clean session-end markers' <<<"$release_state"
grep -Fq 'No world was entered.' <<<"$release_state"
grep -Fq 'The task quit only the Valheim process that it launched, and no Valheim process remained.' <<<"$release_state_flat"

installed_heading="## Test on installed \`$installed_version\`"
installed_queue="$(awk -v heading="$installed_heading" '
  $0 == heading { capture = 1 }
  capture && /^## / && $0 != heading { exit }
  capture { print }
' "$product_review")"
grep -Fxq "$installed_heading" <<<"$installed_queue"
grep -Fq '**Earned-state audio:**' <<<"$installed_queue"

if [[ "$version" == "$installed_version" ]]; then
  review_queue="$installed_queue"
  ! grep -Fq "## After \`$version\` installation" "$product_review"
else
  future_heading="## After \`$version\` installation"
  review_queue="$(awk -v heading="$future_heading" '
    $0 == heading { capture = 1 }
    capture && /^## / && $0 != heading { exit }
    capture { print }
  ' "$product_review")"
  grep -Fxq "$future_heading" <<<"$review_queue"
  ! grep -Fq '**Berry planting:**' <<<"$installed_queue"
  ! grep -Fq '**Club + Lunge Affinity:**' <<<"$installed_queue"
fi
review_queue_flat="$(awk '{$1 = $1; printf "%s ", $0}' <<<"$review_queue")"
grep -Fq '**Berry planting:**' <<<"$review_queue_flat"
grep -Fq '**Portal labels:**' <<<"$review_queue_flat"
grep -Fq '**Comfort summary:**' <<<"$review_queue_flat"
grep -Fq '**Club + Lunge Affinity:**' <<<"$review_queue_flat"
grep -Fq 'Affinity tab' <<<"$review_queue_flat"
grep -Fq 'Spend 1 Wood' <<<"$review_queue_flat"
grep -Fq 'ineligible weapon cannot' <<<"$review_queue_flat"
grep -Fq 'Apply Lunge again as a replacement' <<<"$review_queue_flat"
grep -Fq 'without refunding the materials for the prior Affinity' <<<"$review_queue_flat"
grep -Fq 'Move, equip, store, and drop the Club, then reconnect' <<<"$review_queue_flat"
grep -Fq '10 m/s forward impulse' <<<"$review_queue_flat"
grep -Fq 'vertical velocity to at least +3 m/s' <<<"$review_queue_flat"
grep -Fq 'Grounded Club swings must remain native.' <<<"$review_queue_flat"
grep -Fq 'debug inspect,' <<<"$review_queue_flat"
grep -Fq 'apply, clear, and session-force commands' <<<"$review_queue_flat"
grep -Fq 'peer sees the Lunge' <<<"$review_queue_flat"
grep -Fq '**Cultivator grid selection:**' <<<"$review_queue_flat"
grep -Fq '**Leech spawning:**' <<<"$review_queue_flat"

if grep -Eq 'SHA256SUMS\.txt|"\$release_dir/VERSION"|offers? to install future stable updates|releases/latest/download/VERSION' "$release_script"; then
  echo "release flow still publishes automatic-updater artifacts or instructions" >&2
  exit 1
fi

echo "release flow gates and manual distribution checks passed"
