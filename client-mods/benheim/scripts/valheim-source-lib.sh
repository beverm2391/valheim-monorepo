#!/usr/bin/env bash

# Shared, deliberately small plumbing for the cached Valheim source tools.
#
# This file is sourced by the command scripts.  It does not enable shell
# options and it never exits the caller: callers keep ownership of their
# command name, diagnostics, and exit status.  Functions report a failure by
# setting VALHEIM_SOURCE_ERROR and returning non-zero.

valheim_source_error() {
  VALHEIM_SOURCE_ERROR="$*"
  return 1
}

valheim_source_sha256_file() {
  local path="$1"
  local digest

  if [[ ! -f "$path" ]]; then
    valheim_source_error "file not found for SHA-256: $path"
    return 1
  fi

  if command -v shasum >/dev/null 2>&1; then
    digest="$(shasum -a 256 "$path" | awk '{print $1}')" || {
      valheim_source_error "could not compute SHA-256 for: $path"
      return 1
    }
  elif command -v sha256sum >/dev/null 2>&1; then
    digest="$(sha256sum "$path" | awk '{print $1}')" || {
      valheim_source_error "could not compute SHA-256 for: $path"
      return 1
    }
  else
    valheim_source_error "SHA-256 tool not found; install shasum or sha256sum"
    return 1
  fi

  if [[ ! "$digest" =~ ^[[:xdigit:]]{64}$ ]]; then
    valheim_source_error "SHA-256 tool returned an invalid digest for: $path"
    return 1
  fi

  printf '%s' "$digest" | tr '[:upper:]' '[:lower:]'
}

valheim_source_sha256_text() {
  local value="$1"
  local digest

  if command -v shasum >/dev/null 2>&1; then
    digest="$(printf '%s' "$value" | shasum -a 256 | awk '{print $1}')" || {
      valheim_source_error "could not compute SHA-256 for text"
      return 1
    }
  elif command -v sha256sum >/dev/null 2>&1; then
    digest="$(printf '%s' "$value" | sha256sum | awk '{print $1}')" || {
      valheim_source_error "could not compute SHA-256 for text"
      return 1
    }
  else
    valheim_source_error "SHA-256 tool not found; install shasum or sha256sum"
    return 1
  fi

  if [[ ! "$digest" =~ ^[[:xdigit:]]{64}$ ]]; then
    valheim_source_error "SHA-256 tool returned an invalid text digest"
    return 1
  fi

  printf '%s' "$digest" | tr '[:upper:]' '[:lower:]'
}

valheim_source_resolve_assembly() {
  local game_dir
  local assembly_input
  local assembly_dir

  game_dir="${VALHEIM_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
  assembly_input="${VALHEIM_ASSEMBLY_PATH:-$game_dir/valheim.app/Contents/Resources/Data/Managed/assembly_valheim.dll}"

  if [[ ! -f "$assembly_input" ]]; then
    valheim_source_error "Valheim assembly not found at: $assembly_input"
    return 1
  fi

  assembly_dir="$(cd "$(dirname "$assembly_input")" 2>/dev/null && pwd -P)" || {
    valheim_source_error "could not resolve the Valheim assembly directory: $assembly_input"
    return 1
  }

  VALHEIM_SOURCE_ASSEMBLY_PATH="$assembly_dir/$(basename "$assembly_input")"
  VALHEIM_SOURCE_ASSEMBLY_SHA="$(valheim_source_sha256_file "$VALHEIM_SOURCE_ASSEMBLY_PATH")" || return 1
}

valheim_source_cache_root() {
  local requested_root

  requested_root="${VALHEIM_SOURCE_CACHE_DIR:-${VALHEIM_DECOMPILE_CACHE_DIR:-${TMPDIR:-/tmp}/benheim-valheim-decompile}}"
  if [[ -z "$requested_root" ]]; then
    valheim_source_error "cache root is empty; set VALHEIM_SOURCE_CACHE_DIR or VALHEIM_DECOMPILE_CACHE_DIR"
    return 1
  fi

  if ! mkdir -p "$requested_root" 2>/dev/null; then
    valheim_source_error "could not create cache root: $requested_root"
    return 1
  fi

  VALHEIM_SOURCE_CACHE_ROOT="$(cd "$requested_root" 2>/dev/null && pwd -P)" || {
    valheim_source_error "could not resolve cache root: $requested_root"
    return 1
  }
}

valheim_source_resolve_ilspy() {
  local requested
  local resolved_dir

  if [[ -n "${VALHEIM_ILSPY_PATH:-}" ]]; then
    requested="$VALHEIM_ILSPY_PATH"
    if [[ ! -x "$requested" ]]; then
      valheim_source_error "ILSpy override is not executable: $requested"
      return 1
    fi
  elif command -v ilspycmd >/dev/null 2>&1; then
    requested="$(command -v ilspycmd)"
  elif [[ -x "$HOME/.dotnet/tools/ilspycmd" ]]; then
    requested="$HOME/.dotnet/tools/ilspycmd"
  else
    valheim_source_error "ilspycmd not found; set VALHEIM_ILSPY_PATH, add ilspycmd to PATH, or install it under \$HOME/.dotnet/tools"
    return 1
  fi

  if [[ ! -x "$requested" ]]; then
    valheim_source_error "resolved ILSpy is not executable: $requested"
    return 1
  fi

  resolved_dir="$(cd "$(dirname "$requested")" 2>/dev/null && pwd -P)" || {
    valheim_source_error "could not resolve ILSpy directory: $requested"
    return 1
  }
  VALHEIM_SOURCE_ILSPY_PATH="$resolved_dir/$(basename "$requested")"
}

valheim_source_decompiler_identity() {
  local version_mode="${1:-probe-version}"
  local launcher_sha
  local version_output
  local identity_material
  local identity_sha

  launcher_sha="$(valheim_source_sha256_file "$VALHEIM_SOURCE_ILSPY_PATH")" || return 1

  # --version is part of ilspycmd's stable CLI.  A test double or a wrapper
  # that does not implement it still gets a useful identity from its exact
  # launcher bytes and path; the explicit unknown marker keeps that fallback
  # distinct from a reported version.  The single-type helper can request the
  # no-probe form so resolving a cache hit never invokes a fake or expensive
  # decompiler a second time.
  if [[ "$version_mode" == '--no-version' ]]; then
    version_output='(not-probed)'
  else
    version_output="$("$VALHEIM_SOURCE_ILSPY_PATH" --version 2>/dev/null || true)"
  fi
  if [[ -z "$version_output" ]]; then
    version_output='(unknown)'
  fi
  version_output="$(printf '%s' "$version_output" | tr '\r\n' '  ' | sed 's/[[:space:]][[:space:]]*/ /g; s/^ //; s/ $//')"
  if [[ -z "$version_output" ]]; then
    version_output='(unknown)'
  fi

  identity_material="$(printf 'format=1\npath=%s\nlauncher_sha256=%s\nversion=%s\n' \
    "$VALHEIM_SOURCE_ILSPY_PATH" "$launcher_sha" "$version_output")"
  identity_sha="$(valheim_source_sha256_text "$identity_material")" || return 1

  VALHEIM_SOURCE_ILSPY_LAUNCHER_SHA="$launcher_sha"
  VALHEIM_SOURCE_ILSPY_VERSION="$version_output"
  VALHEIM_SOURCE_ILSPY_ID="ilspy-$identity_sha"
}

valheim_source_project_root() {
  local assembly_sha="$1"
  local decompiler_id="$2"

  if [[ ! "$assembly_sha" =~ ^[[:xdigit:]]{64}$ ]]; then
    valheim_source_error "invalid assembly SHA-256: $assembly_sha"
    return 1
  fi
  if [[ ! "$decompiler_id" =~ ^ilspy-[[:xdigit:]]{64}$ ]]; then
    valheim_source_error "invalid decompiler identity: $decompiler_id"
    return 1
  fi

  printf '%s/%s/projects/%s' "$VALHEIM_SOURCE_CACHE_ROOT" "$assembly_sha" "$decompiler_id"
}

valheim_source_lock_acquire() {
  local lock_path="$1"
  local timeout_seconds="${VALHEIM_SOURCE_LOCK_TIMEOUT_SECONDS:-120}"
  local started
  local now

  if [[ ! "$timeout_seconds" =~ ^[0-9]+$ ]] || (( timeout_seconds < 1 )); then
    valheim_source_error "VALHEIM_SOURCE_LOCK_TIMEOUT_SECONDS must be a positive integer"
    return 1
  fi

  mkdir -p "$(dirname "$lock_path")" || {
    valheim_source_error "could not create cache lock directory: $(dirname "$lock_path")"
    return 1
  }

  started="$(date +%s)"
  while ! mkdir "$lock_path" 2>/dev/null; do
    now="$(date +%s)"
    if (( now - started >= timeout_seconds )); then
      valheim_source_error "cache lock is still held after ${timeout_seconds}s: $lock_path; inspect its owner and remove the stale lock only when no ensure command is running"
      return 1
    fi
    sleep 1
  done

  if ! printf '%s\n' "$$" > "$lock_path/pid"; then
    rmdir "$lock_path" 2>/dev/null || true
    valheim_source_error "could not record cache lock owner: $lock_path"
    return 1
  fi

  VALHEIM_SOURCE_LOCK_PATH="$lock_path"
}

valheim_source_lock_release() {
  local lock_path="${VALHEIM_SOURCE_LOCK_PATH:-}"
  if [[ -z "$lock_path" ]]; then
    return 0
  fi

  rm -f "$lock_path/pid"
  rmdir "$lock_path" 2>/dev/null || true
  VALHEIM_SOURCE_LOCK_PATH=''
}

valheim_source_tree_complete() {
  local tree_path="$1"
  local expected_sha="${2:-}"
  local expected_id="${3:-}"
  local manifest="$tree_path/.benheim/manifest"

  [[ -d "$tree_path" ]] || return 1
  [[ -e "$tree_path/.benheim/COMPLETE" ]] || return 1
  [[ -s "$tree_path/.benheim/types.txt" ]] || return 1
  [[ -s "$manifest" ]] || return 1

  valheim_source_project_output_complete "$tree_path" || return 1

  if [[ -n "$expected_sha" ]] && ! grep -Fqx "assembly_sha256=$expected_sha" "$manifest"; then
    return 1
  fi
  if [[ -n "$expected_id" ]] && ! grep -Fqx "decompiler_id=$expected_id" "$manifest"; then
    return 1
  fi
  return 0
}

valheim_source_project_output_complete() {
  local tree_path="$1"
  local project_count=0
  local source_count=0
  local candidate

  [[ -d "$tree_path" ]] || return 1
  while IFS= read -r candidate; do
    project_count=$((project_count + 1))
  done < <(find "$tree_path" -type f -name '*.csproj' -print)
  (( project_count == 1 )) || return 1

  while IFS= read -r candidate; do
    source_count=$((source_count + 1))
  done < <(find "$tree_path" -type f -name '*.cs' -print)
  (( source_count > 0 )) || return 1
  return 0
}

valheim_source_emit_evidence() {
  local prefix="$1"
  printf '%s: assembly=%s\n' "$prefix" "$VALHEIM_SOURCE_ASSEMBLY_PATH" >&2
  printf '%s: sha256=%s\n' "$prefix" "$VALHEIM_SOURCE_ASSEMBLY_SHA" >&2
  printf '%s: ilspy=%s\n' "$prefix" "$VALHEIM_SOURCE_ILSPY_PATH" >&2
  printf '%s: decompiler=%s\n' "$prefix" "$VALHEIM_SOURCE_ILSPY_ID" >&2
}
