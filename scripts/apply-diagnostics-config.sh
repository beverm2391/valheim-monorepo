#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

if ! diagnostics_config_requested; then
  echo "Set VALHEIM_DIAGNOSTICS_CONFIGURE=1 before applying diagnostics." >&2
  exit 1
fi

root="$(repo_root)"
tmp_diagnostics="$(mktemp)"
remote_stage_created=0
cleanup() {
  local status=$?
  rm -f "$tmp_diagnostics"
  if (( remote_stage_created == 1 )); then
    remote_ssh "rm -rf /tmp/valheim-diagnostics" >/dev/null 2>&1 || true
  fi
  return "$status"
}
trap cleanup EXIT

# Render and validate before touching the host. The local config stays
# non-secret; the scoped ingest token enters through the process environment.
render_diagnostics_env "$tmp_diagnostics"

# An empty ingest verifies both scoped credentials before changing a working
# sidecar. A failed preflight leaves all host files and processes untouched.
for destination in BENHEIM INFRA; do
  dataset_key="${destination}_AXIOM_DATASET"
  token_key="${destination}_AXIOM_INGEST_TOKEN"
  if ! curl --fail --silent --show-error --max-time 10 \
    --header "Authorization: Bearer ${!token_key}" \
    --header 'Content-Type: application/json' \
    --data '[]' \
    "${VALHEIM_AXIOM_ENDPOINT%/}/v1/ingest/${!dataset_key}" >/dev/null; then
    echo "${destination} Axiom ingest preflight failed; diagnostics were not changed." >&2
    exit 1
  fi
done

remote_ssh "install -d -m 0700 /tmp/valheim-diagnostics"
remote_stage_created=1
remote_scp "$root/systemd/valheim-diagnostics.service" "/tmp/valheim-diagnostics/valheim-diagnostics.service"
remote_scp "$root/server/forward-valheim-failures.py" "/tmp/valheim-diagnostics/forward-valheim-failures.py"
remote_scp "$root/server/valheim_failure_events.py" "/tmp/valheim-diagnostics/valheim_failure_events.py"
remote_scp "$tmp_diagnostics" "/tmp/valheim-diagnostics/diagnostics.env"

remote_ssh 'bash -s' <<'REMOTE'
set -euo pipefail
work=/tmp/valheim-diagnostics
previous="$work/previous"
install -d -m 0700 "$previous"
previously_active=0
previously_enabled=0
if systemctl is-active --quiet valheim-diagnostics.service; then
  previously_active=1
fi
if systemctl is-enabled --quiet valheim-diagnostics.service; then
  previously_enabled=1
fi
paths=(
  /etc/valheim/diagnostics.env
  /etc/systemd/system/valheim-diagnostics.service
  /usr/local/bin/valheim-forward-failures
  /usr/local/bin/valheim_failure_events.py
)
for index in "${!paths[@]}"; do
  if [[ -f "${paths[$index]}" ]]; then
    cp -p "${paths[$index]}" "$previous/$index"
  else
    : > "$previous/$index.absent"
  fi
done
finish() {
  status=$?
  if (( status != 0 )); then
    for index in "${!paths[@]}"; do
      if [[ -f "$previous/$index.absent" ]]; then
        rm -f "${paths[$index]}"
      else
        cp -p "$previous/$index" "${paths[$index]}"
      fi
    done
    systemctl daemon-reload
    if (( previously_enabled == 0 )); then
      systemctl disable valheim-diagnostics.service || true
    fi
    if (( previously_active == 1 )); then
      systemctl restart valheim-diagnostics.service || true
    else
      systemctl stop valheim-diagnostics.service || true
    fi
  fi
  rm -rf "$work"
  exit "$status"
}
trap finish EXIT

install -m 0600 -o root -g root "$work/diagnostics.env" /etc/valheim/diagnostics.env
install -m 0644 "$work/valheim-diagnostics.service" /etc/systemd/system/valheim-diagnostics.service
install -m 0755 "$work/forward-valheim-failures.py" /usr/local/bin/valheim-forward-failures
install -m 0644 "$work/valheim_failure_events.py" /usr/local/bin/valheim_failure_events.py
systemctl daemon-reload
systemctl enable valheim-diagnostics.service
systemctl restart valheim-diagnostics.service
systemctl is-active --quiet valheim-diagnostics.service
REMOTE
remote_stage_created=0

echo "Applied Valheim diagnostics on $(ssh_target) without restarting valheim.service"
