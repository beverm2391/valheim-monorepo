#!/usr/bin/env bash
set -euo pipefail

# Produces deliberately secret-bearing private-test installers. The ordinary
# package-all.sh path never reads these variables and never includes this file.
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dll="$root/src/bin/Release/netstandard2.1/BenheimQoL.dll"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
source_commit="${BENHEIM_QOL_SOURCE_COMMIT:-}"
dist="$root/dist"
endpoint="${BENHEIM_AXIOM_ENDPOINT:-https://us-east-1.aws.edge.axiom.co}"
dataset="${BENHEIM_AXIOM_DATASET:-}"
token="${BENHEIM_AXIOM_INGEST_TOKEN:-}"
mac_package="$dist/Benheim-PRIVATE-TEST-macOS-$version.zip"
windows_package="$dist/Benheim-PRIVATE-TEST-Windows-$version.zip"
temp_dir="$(mktemp -d)"
config="$temp_dir/PRIVATE-TEST-DIAGNOSTICS.cfg"
complete=0

if repo_root="$(git -C "$root" rev-parse --show-toplevel 2>/dev/null)"; then
  if [[ -n "$(git -C "$repo_root" status --porcelain --untracked-files=normal)" ]]; then
    echo "Private-test packages require a clean committed repository tree." >&2
    exit 1
  fi
  committed_head="$(git -C "$repo_root" rev-parse HEAD)"
  if [[ -n "$source_commit" && "$source_commit" != "$committed_head" ]]; then
    echo "BENHEIM_QOL_SOURCE_COMMIT does not match the committed repository HEAD." >&2
    exit 1
  fi
  source_commit="$committed_head"
fi

if [[ ! "$source_commit" =~ ^[0-9a-f]{40,64}$ ]]; then
  echo "Private-test packages require an exact source commit." >&2
  exit 1
fi

cleanup() {
  rm -rf "$temp_dir" \
    "$dist/Benheim-PRIVATE-TEST-macOS-$version" \
    "$dist/Benheim-PRIVATE-TEST-Windows-$version"
  if [[ "$complete" != "1" ]]; then
    rm -f "$mac_package" "$windows_package"
  fi
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
if [[ ! "$endpoint" =~ ^https://[^/?#]+$ ]]; then
  echo "BENHEIM_AXIOM_ENDPOINT must be an HTTPS Axiom edge origin without a path." >&2
  exit 1
fi

rm -f "$mac_package" "$windows_package"
"$root/scripts/verify.sh"
if [[ ! -f "$dll" ]]; then
  echo "The verified Benheim Release DLL was not found at: $dll" >&2
  exit 1
fi

build_id="sha256:$(shasum -a 256 "$dll" | awk '{print $1}')"
umask 077
printf '%s\n' \
  'BENHEIM_PRIVATE_DIAGNOSTICS_V1' \
  "endpoint=$endpoint" \
  "dataset=$dataset" \
  "token=$token" \
  "build_id=$build_id" > "$config"

BENHEIM_QOL_DLL="$dll" \
BENHEIM_QOL_DIST="$dist" \
BENHEIM_QOL_SKIP_BUILD=1 \
BENHEIM_QOL_PRIVATE_DIAGNOSTICS_CONFIG="$config" \
BENHEIM_QOL_SOURCE_COMMIT="$source_commit" \
  "$root/scripts/package-macos.sh"
BENHEIM_QOL_DLL="$dll" \
BENHEIM_QOL_DIST="$dist" \
BENHEIM_QOL_SKIP_BUILD=1 \
BENHEIM_QOL_PRIVATE_DIAGNOSTICS_CONFIG="$config" \
BENHEIM_QOL_SOURCE_COMMIT="$source_commit" \
  "$root/scripts/package-windows.sh"

complete=1
