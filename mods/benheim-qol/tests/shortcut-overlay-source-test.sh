#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_file="$root/src/Shortcuts/ShortcutOverlay.cs"

hidden_guard_line="$(grep -nF 'if (!visible)' "$source_file" | head -n 1 | cut -d: -f1)"
style_init_line="$(grep -nF 'EnsureStyles();' "$source_file" | head -n 1 | cut -d: -f1)"
preload_line="$(grep -nF 'PreloadTextOnce();' "$source_file" | head -n 1 | cut -d: -f1)"
style_cache_line="$(grep -nF 'if (titleStyle != null)' "$source_file" | head -n 1 | cut -d: -f1)"
texture_create_line="$(grep -nF 'panelBackground = new Texture2D' "$source_file" | head -n 1 | cut -d: -f1)"

if [[ -z "$hidden_guard_line" || -z "$style_init_line" || -z "$preload_line" ||
      "$style_init_line" -ge "$preload_line" || "$preload_line" -ge "$hidden_guard_line" ]]; then
  printf 'shortcut overlay must preload styles and text before its hidden fast path\n' >&2
  exit 1
fi

if [[ -z "$style_cache_line" || -z "$texture_create_line" ||
      "$style_cache_line" -ge "$texture_create_line" ]]; then
  printf 'shortcut overlay must reuse its styles and texture after first initialization\n' >&2
  exit 1
fi

grep -Fq 'if (preloaded || Event.current.type != EventType.Repaint)' "$source_file"
grep -Fq 'panel_preloaded' "$source_file"
grep -Fq '"Inventory"' "$source_file"
grep -Fq '"Build & Repair"' "$source_file"
grep -Fq '"Farming"' "$source_file"
grep -Fq '"Travel"' "$source_file"
grep -Fq '"Combat & Skills"' "$source_file"
grep -Fq 'GUILayout.BeginScrollView(' "$source_file"
grep -Fq 'GUILayout.EndScrollView();' "$source_file"

printf 'shortcut overlay preload, cache, and semantic-group checks passed\n'
