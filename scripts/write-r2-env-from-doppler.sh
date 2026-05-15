#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="$root/r2.env"

required=(
  VALHEIM_R2_ACCOUNT_ID
  VALHEIM_R2_BUCKET
  VALHEIM_R2_ACCESS_KEY_ID
  VALHEIM_R2_SECRET_ACCESS_KEY
)

tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT

for key in "${required[@]}"; do
  value="$(doppler secrets get "$key" --plain -p main-v1 -c prd)"
  if [[ -z "$value" ]]; then
    echo "Missing Doppler secret: $key" >&2
    exit 1
  fi
  printf '%s=%q\n' "$key" "$value" >> "$tmp"
done

prefix="${VALHEIM_R2_PREFIX:-benheim}"
printf 'VALHEIM_R2_PREFIX=%q\n' "$prefix" >> "$tmp"

install -m 0600 "$tmp" "$out"
echo "Wrote $out"

