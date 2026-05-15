#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

root="$(repo_root)"
tmp_env="$(mktemp)"
trap 'rm -f "$tmp_env"' EXIT

cat > "$tmp_env" <<EOF
VALHEIM_SERVER_NAME=${VALHEIM_SERVER_NAME}
VALHEIM_WORLD_NAME=${VALHEIM_WORLD_NAME}
VALHEIM_PASSWORD=${VALHEIM_PASSWORD}
VALHEIM_PORT=${VALHEIM_PORT}
VALHEIM_PUBLIC=${VALHEIM_PUBLIC:-1}
VALHEIM_CROSSPLAY=${VALHEIM_CROSSPLAY:-0}
VALHEIM_BACKUP_PREFIX=${VALHEIM_BACKUP_PREFIX:-valheim}
EOF

remote_ssh "mkdir -p /tmp/valheim-server"
remote_scp "$root/systemd/valheim.service" "/tmp/valheim-server/valheim.service"
remote_scp "$tmp_env" "/tmp/valheim-server/server.env"
if [[ -f "$root/r2.env" ]]; then
  remote_scp "$root/r2.env" "/tmp/valheim-server/r2.env"
else
  remote_ssh "rm -f /tmp/valheim-server/r2.env"
fi

remote_ssh 'bash -s' <<'REMOTE'
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive
dpkg --add-architecture i386
echo steamcmd steam/question select "I AGREE" | debconf-set-selections
echo steamcmd steam/license note "" | debconf-set-selections
apt-get update
apt-get install -y ca-certificates curl libatomic1 libpulse0 steamcmd tar unzip
if ! command -v rclone >/dev/null 2>&1; then
  curl -fsSL https://rclone.org/install.sh | bash
fi

id -u valheim >/dev/null 2>&1 || useradd --system --create-home --home-dir /var/lib/valheim --shell /usr/sbin/nologin valheim

install -d -o valheim -g valheim /opt/valheim/server
install -d -o valheim -g valheim /var/lib/valheim/worlds_local
install -d -o valheim -g valheim /var/backups/valheim
install -d -m 0755 /etc/valheim

install -m 0640 -o root -g valheim /tmp/valheim-server/server.env /etc/valheim/server.env
if [[ -f /tmp/valheim-server/r2.env ]]; then
  install -m 0640 -o root -g valheim /tmp/valheim-server/r2.env /etc/valheim/r2.env
fi
install -m 0644 /tmp/valheim-server/valheim.service /etc/systemd/system/valheim.service

cat > /usr/local/bin/valheim-start <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

: "${VALHEIM_SERVER_NAME:?missing VALHEIM_SERVER_NAME}"
: "${VALHEIM_WORLD_NAME:?missing VALHEIM_WORLD_NAME}"
: "${VALHEIM_PASSWORD:?missing VALHEIM_PASSWORD}"
: "${VALHEIM_PORT:=2456}"
: "${VALHEIM_PUBLIC:=1}"
: "${VALHEIM_CROSSPLAY:=0}"

args=(
  -name "$VALHEIM_SERVER_NAME"
  -port "$VALHEIM_PORT"
  -world "$VALHEIM_WORLD_NAME"
  -password "$VALHEIM_PASSWORD"
  -public "$VALHEIM_PUBLIC"
  -savedir /var/lib/valheim
)

if [[ "$VALHEIM_CROSSPLAY" == "1" ]]; then
  args+=(-crossplay)
fi

export LD_LIBRARY_PATH="./linux64:${LD_LIBRARY_PATH:-}"
export SteamAppId=892970

exec /opt/valheim/server/valheim_server.x86_64 "${args[@]}"
EOF
chmod 0755 /usr/local/bin/valheim-start

cat > /usr/local/bin/valheim-update <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
sudo -u valheim /usr/games/steamcmd +force_install_dir /opt/valheim/server +login anonymous +app_update 896660 validate +quit
EOF
chmod 0755 /usr/local/bin/valheim-update

cat > /usr/local/bin/valheim-backup <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
src=/var/lib/valheim/worlds_local
dest=/var/backups/valheim/worlds-$stamp.tar.gz
tar -C "$src" -czf "$dest" .
find /var/backups/valheim -name 'worlds-*.tar.gz' -type f -mtime +14 -delete
echo "$dest"
EOF
chmod 0755 /usr/local/bin/valheim-backup

cat > /usr/local/bin/valheim-r2-upload <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

backup_path="${1:-}"
if [[ -z "$backup_path" || ! -f "$backup_path" ]]; then
  echo "Usage: valheim-r2-upload /path/to/worlds-*.tar.gz" >&2
  exit 1
fi

if [[ ! -f /etc/valheim/r2.env ]]; then
  echo "Skipping R2 upload: /etc/valheim/r2.env not configured"
  exit 0
fi

set -a
# shellcheck disable=SC1091
source /etc/valheim/r2.env
set +a

: "${VALHEIM_R2_ACCOUNT_ID:?missing VALHEIM_R2_ACCOUNT_ID}"
: "${VALHEIM_R2_BUCKET:?missing VALHEIM_R2_BUCKET}"
: "${VALHEIM_R2_ACCESS_KEY_ID:?missing VALHEIM_R2_ACCESS_KEY_ID}"
: "${VALHEIM_R2_SECRET_ACCESS_KEY:?missing VALHEIM_R2_SECRET_ACCESS_KEY}"
: "${VALHEIM_R2_PREFIX:=valheim}"

key="${VALHEIM_R2_PREFIX%/}/$(basename "$backup_path")"
endpoint="https://${VALHEIM_R2_ACCOUNT_ID}.r2.cloudflarestorage.com"

rclone copyto "$backup_path" ":s3:${VALHEIM_R2_BUCKET}/${key}" \
  --s3-provider Cloudflare \
  --s3-access-key-id "$VALHEIM_R2_ACCESS_KEY_ID" \
  --s3-secret-access-key "$VALHEIM_R2_SECRET_ACCESS_KEY" \
  --s3-endpoint "$endpoint" \
  --s3-region auto \
  --s3-no-check-bucket \
  --stats-one-line \
  --stats 0

echo "Uploaded s3://${VALHEIM_R2_BUCKET}/${key}"
EOF
chmod 0755 /usr/local/bin/valheim-r2-upload

cat > /usr/local/bin/valheim-backup-and-upload <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
backup_path="$(valheim-backup)"
valheim-r2-upload "$backup_path"
EOF
chmod 0755 /usr/local/bin/valheim-backup-and-upload

cat > /etc/systemd/system/valheim-backup.service <<'EOF'
[Unit]
Description=Back up Valheim worlds

[Service]
Type=oneshot
ExecStart=/usr/local/bin/valheim-backup-and-upload
EOF

cat > /etc/systemd/system/valheim-backup.timer <<'EOF'
[Unit]
Description=Nightly Valheim world backup

[Timer]
OnCalendar=*-*-* 08:00:00 UTC
Persistent=true

[Install]
WantedBy=timers.target
EOF

/usr/local/bin/valheim-update
chown -R valheim:valheim /opt/valheim /var/lib/valheim
systemctl daemon-reload
systemctl enable valheim.service valheim-backup.timer
systemctl restart valheim-backup.timer
REMOTE

echo "Installed Valheim server on $(ssh_target)"
