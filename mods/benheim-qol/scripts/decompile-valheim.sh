#!/usr/bin/env bash
set -euo pipefail

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

game_dir="${VALHEIM_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
assembly_input="${VALHEIM_ASSEMBLY_PATH:-$game_dir/valheim.app/Contents/Resources/Data/Managed/assembly_valheim.dll}"
if [[ ! -f "$assembly_input" ]]; then
  fail "Valheim assembly not found at: $assembly_input"
fi

assembly_dir="$(cd "$(dirname "$assembly_input")" && pwd -P)" \
  || fail "could not resolve the Valheim assembly directory: $assembly_input"
assembly_path="$assembly_dir/$(basename "$assembly_input")"

if command -v shasum >/dev/null 2>&1; then
  assembly_sha="$(shasum -a 256 "$assembly_path" | awk '{print $1}')"
  type_sha="$(printf '%s' "$requested_type" | shasum -a 256 | awk '{print $1}')"
elif command -v sha256sum >/dev/null 2>&1; then
  assembly_sha="$(sha256sum "$assembly_path" | awk '{print $1}')"
  type_sha="$(printf '%s' "$requested_type" | sha256sum | awk '{print $1}')"
else
  fail "SHA-256 tool not found; install shasum or sha256sum"
fi

cache_root="${VALHEIM_DECOMPILE_CACHE_DIR:-${TMPDIR:-/tmp}/benheim-valheim-decompile}"
assembly_cache="$cache_root/$assembly_sha"
cached_source="$assembly_cache/$type_sha.cs"

cache_status="miss"
if [[ -s "$cached_source" ]]; then
  cache_status="hit"
fi

printf 'decompile-valheim: assembly=%s\n' "$assembly_path" >&2
printf 'decompile-valheim: sha256=%s\n' "$assembly_sha" >&2
printf 'decompile-valheim: type=%s\n' "$requested_type" >&2
printf 'decompile-valheim: cache=%s\n' "$cache_status" >&2

if [[ "$cache_status" == "hit" ]]; then
  cat "$cached_source"
  exit 0
fi

if [[ -n "${VALHEIM_ILSPY_PATH:-}" ]]; then
  ilspy="$VALHEIM_ILSPY_PATH"
  if [[ ! -x "$ilspy" ]]; then
    fail "ILSpy override is not executable: $ilspy"
  fi
elif command -v ilspycmd >/dev/null 2>&1; then
  ilspy="$(command -v ilspycmd)"
elif [[ -x "$HOME/.dotnet/tools/ilspycmd" ]]; then
  ilspy="$HOME/.dotnet/tools/ilspycmd"
else
  fail "ilspycmd not found; set VALHEIM_ILSPY_PATH, add ilspycmd to PATH, or install it under \$HOME/.dotnet/tools"
fi

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
