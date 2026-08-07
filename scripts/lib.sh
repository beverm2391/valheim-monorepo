#!/usr/bin/env bash
set -euo pipefail

repo_root() {
  cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd
}

secret_config_keys=(
  HETZNER_TOKEN
  HCLOUD_TOKEN
  TAILSCALE_AUTHKEY
  VALHEIM_PASSWORD
  VALHEIM_R2_ACCESS_KEY_ID
  VALHEIM_R2_SECRET_ACCESS_KEY
)

reject_secret_assignments() {
  local env_file=$1
  local key

  for key in "${secret_config_keys[@]}"; do
    if grep -Eq "^[[:space:]]*(export[[:space:]]+)?${key}[[:space:]]*=" "$env_file"; then
      echo "Secret assignment ${key} is not allowed in $env_file; provide it in the process environment." >&2
      exit 1
    fi
  done
}

load_config() {
  local root key index
  local secret_was_set=()
  local secret_values=()
  root="$(repo_root)"
  local env_file="${VALHEIM_ENV_FILE:-$root/server.env}"

  if [[ ! -f "$env_file" ]]; then
    echo "Missing config file: $env_file" >&2
    echo "Copy examples/server.env.example to server.env and edit it." >&2
    exit 1
  fi

  reject_secret_assignments "$env_file"

  for key in "${secret_config_keys[@]}"; do
    if declare -p "$key" >/dev/null 2>&1; then
      secret_was_set+=(1)
      secret_values+=("${!key}")
    else
      secret_was_set+=(0)
      secret_values+=("")
    fi
  done

  set -a
  # shellcheck disable=SC1090
  source "$env_file"
  set +a

  for index in "${!secret_config_keys[@]}"; do
    key=${secret_config_keys[$index]}
    if [[ ${secret_was_set[$index]} == 0 ]]; then
      if declare -p "$key" >/dev/null 2>&1; then
        echo "Secret assignment ${key} is not allowed in $env_file; provide it in the process environment." >&2
        exit 1
      fi
    elif ! declare -p "$key" >/dev/null 2>&1 || [[ ${!key} != "${secret_values[$index]}" ]]; then
      echo "Secret assignment ${key} is not allowed in $env_file; provide it in the process environment." >&2
      exit 1
    fi
  done

  : "${HETZNER_SERVER_NAME:?Set HETZNER_SERVER_NAME}"
  : "${VALHEIM_SERVER_NAME:?Set VALHEIM_SERVER_NAME}"
  : "${VALHEIM_WORLD_NAME:?Set VALHEIM_WORLD_NAME}"
  : "${VALHEIM_PORT:=2456}"
  : "${SSH_USER:=root}"
  : "${VALHEIM_R2_CONFIGURE:=0}"
  case "$VALHEIM_R2_CONFIGURE" in
    0|1) ;;
    *)
      echo "Invalid VALHEIM_R2_CONFIGURE: expected 0 or 1." >&2
      exit 1
      ;;
  esac
}

require_server_password() {
  if [[ -z "${VALHEIM_PASSWORD:-}" ]]; then
    echo "Missing VALHEIM_PASSWORD in the process environment." >&2
    return 1
  fi
}

r2_config_requested() {
  [[ "${VALHEIM_R2_CONFIGURE:-0}" == 1 ]]
}

require_r2_config() {
  if [[ -z "${VALHEIM_R2_ACCOUNT_ID:-}" ]]; then
    echo "Missing VALHEIM_R2_ACCOUNT_ID in server.env when R2 is enabled." >&2
    return 1
  fi
  if [[ -z "${VALHEIM_R2_BUCKET:-}" ]]; then
    echo "Missing VALHEIM_R2_BUCKET in server.env when R2 is enabled." >&2
    return 1
  fi
  if [[ -z "${VALHEIM_R2_ACCESS_KEY_ID:-}" ]]; then
    echo "Missing VALHEIM_R2_ACCESS_KEY_ID in the process environment when R2 is enabled." >&2
    return 1
  fi
  if [[ -z "${VALHEIM_R2_SECRET_ACCESS_KEY:-}" ]]; then
    echo "Missing VALHEIM_R2_SECRET_ACCESS_KEY in the process environment when R2 is enabled." >&2
    return 1
  fi
}

render_server_env() {
  local destination=$1

  require_server_password
  : > "$destination"
  chmod 0600 "$destination"
  printf 'VALHEIM_SERVER_NAME=%q\n' "$VALHEIM_SERVER_NAME" >> "$destination"
  printf 'VALHEIM_WORLD_NAME=%q\n' "$VALHEIM_WORLD_NAME" >> "$destination"
  printf 'VALHEIM_PASSWORD=%q\n' "$VALHEIM_PASSWORD" >> "$destination"
  printf 'VALHEIM_PORT=%q\n' "$VALHEIM_PORT" >> "$destination"
  printf 'VALHEIM_PUBLIC=%q\n' "${VALHEIM_PUBLIC:-1}" >> "$destination"
  printf 'VALHEIM_CROSSPLAY=%q\n' "${VALHEIM_CROSSPLAY:-0}" >> "$destination"
  printf 'VALHEIM_MODDED=%q\n' "${VALHEIM_MODDED:-0}" >> "$destination"
  printf 'VALHEIM_PORTALS=%q\n' "${VALHEIM_PORTALS:-}" >> "$destination"
  printf 'VALHEIM_SKILL_GAIN_RATE=%q\n' "${VALHEIM_SKILL_GAIN_RATE:-}" >> "$destination"
  printf 'VALHEIM_SKILL_REDUCTION_RATE=%q\n' "${VALHEIM_SKILL_REDUCTION_RATE:-}" >> "$destination"
  printf 'VALHEIM_BACKUP_PREFIX=%q\n' "${VALHEIM_BACKUP_PREFIX:-valheim}" >> "$destination"
}

render_r2_env() {
  local destination=$1

  require_r2_config
  : > "$destination"
  chmod 0600 "$destination"
  printf 'VALHEIM_R2_ACCOUNT_ID=%q\n' "$VALHEIM_R2_ACCOUNT_ID" >> "$destination"
  printf 'VALHEIM_R2_BUCKET=%q\n' "$VALHEIM_R2_BUCKET" >> "$destination"
  printf 'VALHEIM_R2_ACCESS_KEY_ID=%q\n' "$VALHEIM_R2_ACCESS_KEY_ID" >> "$destination"
  printf 'VALHEIM_R2_SECRET_ACCESS_KEY=%q\n' "$VALHEIM_R2_SECRET_ACCESS_KEY" >> "$destination"
  printf 'VALHEIM_R2_PREFIX=%q\n' "${VALHEIM_R2_PREFIX:-benheim}" >> "$destination"
}

hcloud_cmd() {
  local token="${HETZNER_TOKEN:-${HCLOUD_TOKEN:-}}"
  if [[ -n "$token" ]]; then
    HCLOUD_TOKEN="$token" hcloud "$@"
  elif [[ -n "${HCLOUD_CONTEXT:-}" ]]; then
    hcloud --context "$HCLOUD_CONTEXT" "$@"
  else
    echo "Missing Hetzner auth. Set HETZNER_TOKEN/HCLOUD_TOKEN in the process environment or configure HCLOUD_CONTEXT." >&2
    exit 1
  fi
}

server_ip() {
  hcloud_cmd server ip "$HETZNER_SERVER_NAME"
}

ssh_target() {
  echo "${SSH_USER}@${SSH_HOST:-$(server_ip)}"
}

ssh_args() {
  local args=(-o StrictHostKeyChecking=accept-new)
  if [[ -n "${SSH_KEY_PATH:-}" ]]; then
    args+=(-i "$SSH_KEY_PATH")
  fi
  printf '%q ' "${args[@]}"
}

remote_ssh() {
  local target
  target="$(ssh_target)"
  if [[ -n "${SSH_KEY_PATH:-}" ]]; then
    ssh -i "$SSH_KEY_PATH" -o StrictHostKeyChecking=accept-new "$target" "$@"
  else
    ssh -o StrictHostKeyChecking=accept-new "$target" "$@"
  fi
}

remote_scp() {
  local src=$1
  local dest=$2
  local target
  target="$(ssh_target)"
  if [[ -n "${SSH_KEY_PATH:-}" ]]; then
    scp -i "$SSH_KEY_PATH" -o StrictHostKeyChecking=accept-new "$src" "$target:$dest"
  else
    scp -o StrictHostKeyChecking=accept-new "$src" "$target:$dest"
  fi
}
