#!/usr/bin/env bash
set -euo pipefail

repo_root() {
  cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd
}

load_config() {
  local root
  root="$(repo_root)"
  local env_file="${VALHEIM_ENV_FILE:-$root/server.env}"

  if [[ ! -f "$env_file" ]]; then
    echo "Missing config file: $env_file" >&2
    echo "Copy examples/server.env.example to server.env and edit it." >&2
    exit 1
  fi

  set -a
  # shellcheck disable=SC1090
  source "$env_file"
  set +a

  : "${HETZNER_SERVER_NAME:?Set HETZNER_SERVER_NAME}"
  : "${VALHEIM_SERVER_NAME:?Set VALHEIM_SERVER_NAME}"
  : "${VALHEIM_WORLD_NAME:?Set VALHEIM_WORLD_NAME}"
  : "${VALHEIM_PASSWORD:?Set VALHEIM_PASSWORD}"
  : "${VALHEIM_PORT:=2456}"
  : "${SSH_USER:=root}"
}

hcloud_cmd() {
  if [[ -n "${HCLOUD_CONTEXT:-}" ]]; then
    hcloud --context "$HCLOUD_CONTEXT" "$@"
  else
    hcloud "$@"
  fi
}

server_ip() {
  hcloud_cmd server ip "$HETZNER_SERVER_NAME"
}

ssh_target() {
  echo "${SSH_USER}@$(server_ip)"
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

