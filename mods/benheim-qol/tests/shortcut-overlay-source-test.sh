#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_file="$root/src/Shortcuts/ShortcutOverlay.cs"

hidden_guard_line="$(grep -nF 'if (!visible)' "$source_file" | head -n 1 | cut -d: -f1)"
style_init_line="$(grep -nF 'EnsureStyles();' "$source_file" | head -n 1 | cut -d: -f1)"
style_cache_line="$(grep -nF 'if (titleStyle != null)' "$source_file" | head -n 1 | cut -d: -f1)"
texture_create_line="$(grep -nF 'panelBackground = new Texture2D' "$source_file" | head -n 1 | cut -d: -f1)"

if [[ -z "$hidden_guard_line" || -z "$style_init_line" ||
      "$hidden_guard_line" -ge "$style_init_line" ]]; then
  printf 'shortcut overlay must return while hidden before initializing or laying out UI\n' >&2
  exit 1
fi

if [[ -z "$style_cache_line" || -z "$texture_create_line" ||
      "$style_cache_line" -ge "$texture_create_line" ]]; then
  printf 'shortcut overlay must reuse its styles and texture after first initialization\n' >&2
  exit 1
fi

printf 'shortcut overlay hidden-path and style-cache checks passed\n'
