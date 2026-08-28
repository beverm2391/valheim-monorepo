#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
# shellcheck source=valheim-source-lib.sh
source "$script_dir/valheim-source-lib.sh"

command_name='ensure-valheim-source'
fail() {
  printf '%s: %s\n' "$command_name" "$1" >&2
  exit 1
}

if [[ "$#" -eq 1 && "$1" == '--help' ]]; then
  cat <<'HELP'
Usage: ensure-valheim-source.sh [--help]

Ensure the complete ILSpy project tree for the installed Valheim assembly is
cached, then print its absolute tree path to standard output.  Evidence and
cache hit/miss status are written to standard error.

Environment:
  VALHEIM_ASSEMBLY_PATH              exact assembly to cache
  VALHEIM_GAME_DIR                   Valheim install root (default: Steam Mac path)
  VALHEIM_ILSPY_PATH                 executable ilspycmd override
  VALHEIM_SOURCE_CACHE_DIR           source cache root
  VALHEIM_DECOMPILE_CACHE_DIR        shared cache root (legacy name)
  VALHEIM_SOURCE_LOCK_TIMEOUT_SECONDS  lock wait limit (default: 120)
HELP
  exit 0
fi
if [[ "$#" -ne 0 ]]; then
  fail "usage: $0 [--help]"
fi

valheim_source_resolve_assembly || fail "$VALHEIM_SOURCE_ERROR"
valheim_source_cache_root || fail "$VALHEIM_SOURCE_ERROR"
valheim_source_resolve_ilspy || fail "$VALHEIM_SOURCE_ERROR"
valheim_source_decompiler_identity || fail "$VALHEIM_SOURCE_ERROR"

project_root="$(valheim_source_project_root "$VALHEIM_SOURCE_ASSEMBLY_SHA" "$VALHEIM_SOURCE_ILSPY_ID")" \
  || fail "$VALHEIM_SOURCE_ERROR"
project_parent="$(dirname "$project_root")"
lock_path="$project_parent/.locks/$VALHEIM_SOURCE_ILSPY_ID.lock"

valheim_source_emit_evidence "$command_name"
printf '%s: tree=%s\n' "$command_name" "$project_root" >&2

if [[ -e "$project_root" ]]; then
  if valheim_source_tree_complete "$project_root" "$VALHEIM_SOURCE_ASSEMBLY_SHA" "$VALHEIM_SOURCE_ILSPY_ID"; then
    printf '%s: cache=hit\n' "$command_name" >&2
    printf '%s\n' "$project_root"
    exit 0
  fi
  fail "cached project path exists but is incomplete or invalid: $project_root; remove only that incomplete entry and retry"
fi

valheim_source_lock_acquire "$lock_path" || fail "$VALHEIM_SOURCE_ERROR"
stage_dir=''
log_dir=''
cleanup() {
  if [[ -n "$stage_dir" && -d "$stage_dir" ]]; then
    rm -rf "$stage_dir"
  fi
  if [[ -n "$log_dir" && -d "$log_dir" ]]; then
    rm -rf "$log_dir"
  fi
  valheim_source_lock_release
}
trap cleanup EXIT

# Re-check after waiting for another caller.  The lock is per assembly and
# decompiler identity, so this is the only publication race that matters.
if [[ -e "$project_root" ]]; then
  if valheim_source_tree_complete "$project_root" "$VALHEIM_SOURCE_ASSEMBLY_SHA" "$VALHEIM_SOURCE_ILSPY_ID"; then
    printf '%s: cache=hit-after-lock\n' "$command_name" >&2
    printf '%s\n' "$project_root"
    exit 0
  fi
  fail "cached project path appeared but is incomplete or invalid: $project_root"
fi

mkdir -p "$project_parent"
stage_dir="$(mktemp -d "$project_parent/.stage.XXXXXX")" \
  || fail "could not create staging directory under: $project_parent"
log_dir="$(mktemp -d "$project_parent/.logs.XXXXXX")" \
  || fail "could not create ILSpy log directory under: $project_parent"

project_stdout="$log_dir/project.stdout"
project_stderr="$log_dir/project.stderr"
if ! "$VALHEIM_SOURCE_ILSPY_PATH" --disable-updatecheck --nested-directories -p -o "$stage_dir" \
  "$VALHEIM_SOURCE_ASSEMBLY_PATH" >"$project_stdout" 2>"$project_stderr"; then
  if [[ -s "$project_stderr" ]]; then
    sed 's/^/ensure-valheim-source: ilspy: /' "$project_stderr" >&2
  fi
  if [[ -s "$project_stdout" ]]; then
    sed 's/^/ensure-valheim-source: ilspy-output: /' "$project_stdout" >&2
  fi
  fail "ILSpy failed to create a complete project tree"
fi

types_stdout="$log_dir/types.stdout"
types_stderr="$log_dir/types.stderr"
if ! "$VALHEIM_SOURCE_ILSPY_PATH" --disable-updatecheck -l cisde \
  "$VALHEIM_SOURCE_ASSEMBLY_PATH" >"$types_stdout" 2>"$types_stderr"; then
  if [[ -s "$types_stderr" ]]; then
    sed 's/^/ensure-valheim-source: ilspy: /' "$types_stderr" >&2
  fi
  if [[ -s "$types_stdout" ]]; then
    sed 's/^/ensure-valheim-source: ilspy-output: /' "$types_stdout" >&2
  fi
  fail "ILSpy failed to list Valheim type metadata"
fi

final_assembly_sha="$(valheim_source_sha256_file "$VALHEIM_SOURCE_ASSEMBLY_PATH")" \
  || fail "$VALHEIM_SOURCE_ERROR"
if [[ "$final_assembly_sha" != "$VALHEIM_SOURCE_ASSEMBLY_SHA" ]]; then
  fail "Valheim assembly changed while ILSpy was reading it; retry to build the new version cache"
fi

if ! valheim_source_project_output_complete "$stage_dir"; then
  fail "ILSpy returned incomplete project output (expected one .csproj and at least one .cs file)"
fi
if [[ ! -s "$types_stdout" ]]; then
  fail "ILSpy returned empty class/interface/struct/delegate/enum metadata"
fi
metadata_rows="$(awk '
  $1 == "Class" || $1 == "Interface" || $1 == "Struct" ||
  $1 == "Delegate" || $1 == "Enum" { if (NF >= 2) count++ }
  END { print count + 0 }
' "$types_stdout")"
if [[ "$metadata_rows" -lt 1 ]]; then
  fail "ILSpy returned no recognizable class/interface/struct/delegate/enum metadata rows"
fi

mkdir -p "$stage_dir/.benheim"
mv -f "$types_stdout" "$stage_dir/.benheim/types.txt"
printf 'format=1\nassembly_sha256=%s\ndecompiler_id=%s\ndecompiler_version=%s\nilspy_launcher_sha256=%s\n' \
  "$VALHEIM_SOURCE_ASSEMBLY_SHA" \
  "$VALHEIM_SOURCE_ILSPY_ID" \
  "$VALHEIM_SOURCE_ILSPY_VERSION" \
  "$VALHEIM_SOURCE_ILSPY_LAUNCHER_SHA" > "$stage_dir/.benheim/manifest"
: > "$stage_dir/.benheim/COMPLETE"

if ! valheim_source_tree_complete "$stage_dir" "$VALHEIM_SOURCE_ASSEMBLY_SHA" "$VALHEIM_SOURCE_ILSPY_ID"; then
  fail "staged ILSpy project failed complete-output validation"
fi
if [[ -e "$project_root" ]]; then
  fail "cache target appeared during publication: $project_root"
fi

# A directory rename is atomic on one filesystem.  COMPLETE is written before
# this move, so readers never observe a published but partial project tree.
mv "$stage_dir" "$project_root" \
  || fail "could not publish complete project tree: $project_root"
stage_dir=''

printf '%s: cache=miss\n' "$command_name" >&2
printf '%s\n' "$project_root"
