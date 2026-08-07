#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() {
  cat >&2 <<'EOF'
Usage:
  scripts/with-ben-secrets.sh config
  scripts/with-ben-secrets.sh install
  scripts/with-ben-secrets.sh hetzner create|destroy

Injects only the Doppler valheim/prd keys required by the selected Benheim
operation. Generic repo scripts remain usable with process-environment inputs.
EOF
  exit 2
}

profile=${1:-}
shift || true
command=()
secrets=()

case "$profile" in
  config|server-config)
    [[ $# -eq 0 ]] || usage
    command=("$root/scripts/apply-server-config.sh")
    secrets=(VALHEIM_PASSWORD)
    # Remote helpers resolve a blank SSH_HOST through Hetzner. Scope the token
    # only when neither an explicit host nor a configured hcloud context can
    # satisfy that lookup.
    # shellcheck source=scripts/lib.sh
    source "$root/scripts/lib.sh"
    load_config
    if [[ -z "${SSH_HOST:-}" && -z "${HCLOUD_CONTEXT:-}" ]]; then
      secrets+=(HETZNER_TOKEN)
    fi
    ;;
  install|full-install)
    [[ $# -eq 0 ]] || usage
    command=("$root/scripts/install-server.sh")
    secrets=(VALHEIM_PASSWORD)
    # shellcheck source=scripts/lib.sh
    source "$root/scripts/lib.sh"
    load_config
    if [[ -z "${SSH_HOST:-}" && -z "${HCLOUD_CONTEXT:-}" ]]; then
      secrets+=(HETZNER_TOKEN)
    fi
    if r2_config_requested; then
      secrets+=(VALHEIM_R2_ACCESS_KEY_ID VALHEIM_R2_SECRET_ACCESS_KEY)
    fi
    ;;
  hetzner)
    [[ $# -eq 1 ]] || usage
    case "$1" in
      create) command=("$root/providers/hetzner/create.sh") ;;
      destroy) command=("$root/providers/hetzner/destroy.sh") ;;
      *) usage ;;
    esac
    secrets=(HETZNER_TOKEN)
    ;;
  *)
    usage
    ;;
esac

scoped=(safe b secrets run --project valheim --config prd)
for key in "${secrets[@]}"; do
  scoped+=(--secret "$key")
done
scoped+=(-- "${command[@]}")
exec "${scoped[@]}"
