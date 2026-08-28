#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
command_name='list-valheim-types'
fail() {
  printf '%s: %s\n' "$command_name" "$1" >&2
  exit 1
}

if [[ "$#" -gt 0 && "$1" == '--help' ]]; then
  if [[ "$#" -ne 1 ]]; then
    fail "usage: $0 [--help] [--kind class|interface|struct|delegate|enum|all] [query]"
  fi
  cat <<'HELP'
Usage: list-valheim-types.sh [--help] [--kind KIND] [QUERY]

Ensure the current complete source tree, then list exact type kind/name rows
from ILSpy's cached class/interface/struct/delegate/enum metadata.  QUERY is
a case-insensitive fixed substring match; the original ILSpy spelling and
kind are printed unchanged.  With no query, all cached rows are printed.

Examples:
  list-valheim-types.sh Character
  list-valheim-types.sh --kind interface net
HELP
  exit 0
fi

kind='all'
query=''
while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --kind)
      [[ "$#" -ge 2 ]] || fail "--kind requires class, interface, struct, delegate, enum, or all"
      kind="$(printf '%s' "$2" | tr '[:upper:]' '[:lower:]')"
      shift 2
      ;;
    --)
      shift
      [[ "$#" -le 1 ]] || fail "only one fuzzy query is accepted"
      if [[ "$#" -eq 1 ]]; then
        query="$1"
      fi
      shift
      ;;
    -* )
      fail "unknown option '$1'; use --help"
      ;;
    *)
      [[ -z "$query" ]] || fail "only one fuzzy query is accepted"
      query="$1"
      shift
      ;;
  esac
done

case "$kind" in
  class|interface|struct|delegate|enum|all) ;;
  *) fail "invalid type kind '$kind'; use class, interface, struct, delegate, enum, or all" ;;
esac

tree_path="$("$script_dir/ensure-valheim-source.sh")" \
  || fail "could not ensure the current complete source tree"
types_file="$tree_path/.benheim/types.txt"
[[ -s "$types_file" ]] || fail "complete source tree has no cached ILSpy type metadata: $tree_path"

printf '%s: tree=%s\n' "$command_name" "$tree_path" >&2
printf '%s: kind=%s\n' "$command_name" "$kind" >&2
if [[ -n "$query" ]]; then
  printf '%s: query=%s\n' "$command_name" "$query" >&2
fi

# ILSpy emits one exact row per entity, e.g. "Class Character".  Keep the
# original row intact instead of constructing a second type index or trying
# to parse C# source.  The tiny awk filter only applies the requested kind and
# case-insensitive fixed substring query.
awk -v requested_kind="$kind" -v requested_query="$query" '
  function lower(value) { return tolower(value) }
  {
    line = $0
    sub(/^[[:space:]]+/, "", line)
    split(line, fields, /[[:space:]]+/)
    entity = lower(fields[1])
    if (entity != "class" && entity != "interface" && entity != "struct" &&
        entity != "delegate" && entity != "enum") {
      next
    }
    if (requested_kind != "all" && entity != requested_kind) {
      next
    }
    if (requested_query != "" && index(lower(line), lower(requested_query)) == 0) {
      next
    }
    print line
  }
' "$types_file"
