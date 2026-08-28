#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
command_name='search-valheim-source'
fail() {
  printf '%s: %s\n' "$command_name" "$1" >&2
  exit 1
}

if [[ "$#" -eq 1 && "$1" == '--help' ]]; then
  cat <<'HELP'
Usage: search-valheim-source.sh [--help] RG-ARGUMENT ...

Ensure the current complete ILSpy project tree, then run rg with the supplied
arguments against that tree.  rg's normal stdout, stderr, and exit status are
preserved; cache/assembly/tree evidence is written to standard error.

Examples:
  search-valheim-source.sh 'class Character'
  search-valheim-source.sh -n -i --glob '*.cs' 'damage'
  search-valheim-source.sh -- '--literal-leading-dash'
HELP
  exit 0
fi
if [[ "$#" -eq 0 ]]; then
  fail "usage: $0 [--help] RG-ARGUMENT ..."
fi
for rg_argument in "$@"; do
  if [[ -z "$rg_argument" ]]; then
    fail "rg arguments must not be empty"
  fi
done
if ! command -v rg >/dev/null 2>&1; then
  fail "rg not found; install ripgrep or add rg to PATH"
fi

tree_path="$("$script_dir/ensure-valheim-source.sh")" \
  || fail "could not ensure the current complete source tree"
printf '%s: tree=%s\n' "$command_name" "$tree_path" >&2

# Keep rg in an if condition so set -e does not rewrite its documented 0
# (match), 1 (no match), and 2 (error) statuses.  The tree path is the final
# operand, leaving all caller-provided options and -- delimiters untouched.
if rg "$@" "$tree_path"; then
  exit 0
else
  exit "$?"
fi
