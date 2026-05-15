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
EOF

remote_ssh "mkdir -p /tmp/valheim-server"
remote_scp "$root/systemd/valheim.service" "/tmp/valheim-server/valheim.service"
remote_scp "$tmp_env" "/tmp/valheim-server/server.env"

remote_ssh 'bash -s' <<'REMOTE'
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive
dpkg --add-architecture i386
echo steamcmd steam/question select "I AGREE" | debconf-set-selections
echo steamcmd steam/license note "" | debconf-set-selections
apt-get update
apt-get install -y ca-certificates curl libatomic1 libpulse0 steamcmd tar

id -u valheim >/dev/null 2>&1 || useradd --system --create-home --home-dir /var/lib/valheim --shell /usr/sbin/nologin valheim

install -d -o valheim -g valheim /opt/valheim/server
install -d -o valheim -g valheim /var/lib/valheim/worlds_local
install -d -o valheim -g valheim /var/backups/valheim
install -d -m 0755 /etc/valheim

install -m 0640 -o root -g valheim /tmp/valheim-server/server.env /etc/valheim/server.env
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

cat > /etc/systemd/system/valheim-backup.service <<'EOF'
[Unit]
Description=Back up Valheim worlds

[Service]
Type=oneshot
ExecStart=/usr/local/bin/valheim-backup
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
