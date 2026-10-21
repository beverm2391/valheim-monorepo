#!/usr/bin/env bash
set -euo pipefail

# Produces deliberately credential-bearing group installers. No package without
# configured diagnostics is distributable.
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_commit="${BENHEIM_QOL_SOURCE_COMMIT:-}"
dist="$root/dist"
endpoint="${BENHEIM_AXIOM_ENDPOINT:-https://us-east-1.aws.edge.axiom.co}"
dataset="${BENHEIM_AXIOM_DATASET:-}"
speech_api_key="${OPENROUTER_API_KEY:-}"
token="${BENHEIM_AXIOM_INGEST_TOKEN:-}"

if repo_root="$(git -C "$root" rev-parse --show-toplevel 2>/dev/null)"; then
  if [[ -n "$(git -C "$repo_root" status --porcelain --untracked-files=normal -- client-mods/benheim shared/benheim-inventory-protocol shared/benheim-resident-protocol tools/valheim-dev/contextual-speech/george.txt)" ]]; then
    echo "Group packages require committed Benheim source." >&2
    exit 1
  fi
  committed_head="$(git -C "$repo_root" rev-parse HEAD)"
  if [[ -n "$source_commit" && "$source_commit" != "$committed_head" ]]; then
    echo "BENHEIM_QOL_SOURCE_COMMIT does not match the committed Benheim source HEAD." >&2
    exit 1
  fi
  source_commit="$committed_head"
else
  echo "Group packages require a Git checkout of the committed source." >&2
  exit 1
fi

if [[ ! "$source_commit" =~ ^[0-9a-f]{40,64}$ ]]; then
  echo "Group packages require an exact source commit." >&2
  exit 1
fi

temp_dir="$(mktemp -d)"
config="$temp_dir/AXIOM-DIAGNOSTICS.cfg"
speech_config="$temp_dir/GEORGE-SPEECH.cfg"
cleanup() {
  rm -rf "$temp_dir"
}
trap cleanup EXIT

if [[ -z "$dataset" || ! "$dataset" =~ ^[A-Za-z0-9_.-]{1,200}$ ]]; then
  echo "Set BENHEIM_AXIOM_DATASET to the private-test Axiom dataset name." >&2
  exit 1
fi
if [[ -z "$token" || ${#token} -gt 1024 || "$token" == *$'\n'* || "$token" == *$'\r'* ]]; then
  echo "Set BENHEIM_AXIOM_INGEST_TOKEN to the dataset-scoped ingest-only token." >&2
  exit 1
fi
if [[ -z "$speech_api_key" || ${#speech_api_key} -gt 1024 || "$speech_api_key" == *[[:space:]]* ]]; then
  echo "Set OPENROUTER_API_KEY to the private-test resident speech credential." >&2
  exit 1
fi
if [[ ! "$endpoint" =~ ^https://[^/?#]+$ ]]; then
  echo "BENHEIM_AXIOM_ENDPOINT must be an HTTPS Axiom edge origin without a path." >&2
  exit 1
fi

# Verify the selected commit once, then package only that export. A shared
# checkout can keep changing without mixing earlier checks, a later DLL, or
# different installer scripts into the candidate. The export has no Git state
# or cached build output; source_commit supplies its provenance to the checks.
source_export="$temp_dir/source"
package_dist="$temp_dir/packages"
mkdir -p "$source_export" "$package_dist"
git -C "$repo_root" archive "$source_commit" | tar -x -C "$source_export"
candidate_root="$source_export/client-mods/benheim"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$candidate_root/src/Plugin.cs")"
if [[ -z "$version" ]]; then
  echo "Could not determine the committed Benheim version." >&2
  exit 1
fi
printf 'package: stage=verify source_commit=%s status=started\n' "$source_commit" >&2
BENHEIM_QOL_SOURCE_COMMIT="$source_commit" "$candidate_root/scripts/verify.sh"
dll="$candidate_root/src/bin/Release/netstandard2.1/BenheimQoL.dll"
if [[ ! -s "$dll" ]]; then
  echo "The verified Benheim Release DLL was not found at: $dll" >&2
  exit 1
fi

# Both platform packages consume these same verified bytes, independent of
# build outputs in the caller's checkout or later work in the export.
install -m 0644 "$dll" "$temp_dir/BenheimQoL.dll"
dll="$temp_dir/BenheimQoL.dll"
build_id="sha256:$(shasum -a 256 "$dll" | awk '{print $1}')"
printf 'package: stage=verify source_commit=%s build_id=%s status=passed\n' "$source_commit" "$build_id" >&2
umask 077
printf '%s\n' \
  'BENHEIM_PRIVATE_DIAGNOSTICS_V1' \
  "endpoint=$endpoint" \
  "dataset=$dataset" \
  "token=$token" \
  "build_id=$build_id" > "$config"
printf '%s\n' \
  'BENHEIM_PRIVATE_SPEECH_V1' \
  "api_key=$speech_api_key" > "$speech_config"

for target in macos windows; do
  BENHEIM_QOL_DLL="$dll" \
  BENHEIM_QOL_DIST="$package_dist" \
  BENHEIM_QOL_SKIP_BUILD=1 \
  BENHEIM_QOL_PRIVATE_DIAGNOSTICS_CONFIG="$config" \
  BENHEIM_QOL_PRIVATE_SPEECH_CONFIG="$speech_config" \
  BENHEIM_QOL_SOURCE_COMMIT="$source_commit" \
    "$candidate_root/scripts/package-$target.sh" >/dev/null
done

# Check the archive payloads, not just the inputs passed to the packagers.
# Publish neither candidate until both have the verified DLL and provenance.
for platform in macOS Windows; do
  package_name="Benheim-$platform-$version"
  archive="$package_dist/$package_name.zip"
  if ! unzip -p "$archive" "$package_name/BenheimQoL.dll" > "$temp_dir/$platform.dll" ||
    ! cmp -s "$dll" "$temp_dir/$platform.dll" ||
    [[ "$(unzip -p "$archive" "$package_name/VERSION")" != "$version" ]] ||
    [[ "$(unzip -p "$archive" "$package_name/SOURCE_COMMIT")" != "$source_commit" ]] ||
    ! unzip -p "$archive" "$package_name/AXIOM-DIAGNOSTICS.cfg" | grep -Fxq "build_id=$build_id"; then
    printf 'package: stage=archive_check platform=%s status=failed\n' "$platform" >&2
    exit 1
  fi
  printf 'package: stage=archive_check platform=%s status=passed\n' "$platform" >&2
done
mkdir -p "$dist"
for platform in macOS Windows; do
  package_name="Benheim-$platform-$version.zip"
  if ! mv -f "$package_dist/$package_name" "$dist/$package_name"; then
    printf 'package: stage=publish platform=%s status=failed\n' "$platform" >&2
    exit 1
  fi
done
printf '%s\n' "$dist/Benheim-macOS-$version.zip" "$dist/Benheim-Windows-$version.zip"
printf 'package: stage=complete source_commit=%s build_id=%s status=passed\n' "$source_commit" "$build_id" >&2
