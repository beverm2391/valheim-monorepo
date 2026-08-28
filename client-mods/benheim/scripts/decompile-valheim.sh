#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
# shellcheck source=valheim-source-lib.sh
source "$script_dir/valheim-source-lib.sh"

fail() {
  printf 'decompile-valheim: %s\n' "$1" >&2
  exit 1
}

if [[ "$#" -ne 1 ]]; then
  fail "usage: $0 <Type>"
fi

requested_type="$1"
type_pattern='^[A-Za-z_][A-Za-z0-9_.+`]*$'
if [[ -z "$requested_type" || ! "$requested_type" =~ $type_pattern ]]; then
  fail "invalid type '$requested_type'; provide one exact Valheim type such as Character"
fi

valheim_source_resolve_assembly || fail "$VALHEIM_SOURCE_ERROR"
assembly_path="$VALHEIM_SOURCE_ASSEMBLY_PATH"
assembly_sha="$VALHEIM_SOURCE_ASSEMBLY_SHA"
type_sha="$(valheim_source_sha256_text "$requested_type")" || fail "$VALHEIM_SOURCE_ERROR"
valheim_source_cache_root || fail "$VALHEIM_SOURCE_ERROR"
cache_root="$VALHEIM_SOURCE_CACHE_ROOT"
valheim_source_resolve_ilspy || fail "$VALHEIM_SOURCE_ERROR"
valheim_source_decompiler_identity --no-version || fail "$VALHEIM_SOURCE_ERROR"
assembly_cache="$cache_root/$assembly_sha/types/$VALHEIM_SOURCE_ILSPY_ID"
cached_source="$assembly_cache/$type_sha.cs"

cache_status="miss"
if [[ -s "$cached_source" ]]; then
  cache_status="hit"
fi

printf 'decompile-valheim: assembly=%s\n' "$assembly_path" >&2
printf 'decompile-valheim: sha256=%s\n' "$assembly_sha" >&2
printf 'decompile-valheim: type=%s\n' "$requested_type" >&2
printf 'decompile-valheim: decompiler=%s\n' "$VALHEIM_SOURCE_ILSPY_ID" >&2
printf 'decompile-valheim: cache=%s\n' "$cache_status" >&2

if [[ "$cache_status" == "hit" ]]; then
  cat "$cached_source"
  exit 0
fi

ilspy="$VALHEIM_SOURCE_ILSPY_PATH"

mkdir -p "$assembly_cache"
stage_file="$(mktemp "$assembly_cache/.${type_sha}.stage.XXXXXX")" \
  || fail "could not create a cache staging file under: $assembly_cache"
ilspy_stderr="${stage_file}.stderr"
cleanup() {
  rm -f "$stage_file" "$ilspy_stderr"
}
trap cleanup EXIT

if ! "$ilspy" --disable-updatecheck -t "$requested_type" "$assembly_path" \
  > "$stage_file" 2> "$ilspy_stderr"; then
  if [[ -s "$ilspy_stderr" ]]; then
    sed 's/^/decompile-valheim: ilspy: /' "$ilspy_stderr" >&2
  fi
  fail "ILSpy failed to decompile type '$requested_type'"
fi

if [[ ! -s "$stage_file" ]]; then
  fail "ILSpy returned empty source for type '$requested_type'"
fi

mv -f "$stage_file" "$cached_source"
cat "$cached_source"
