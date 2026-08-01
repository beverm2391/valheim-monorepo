#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

root="$(repo_root)"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

bepinex_file=BepInExPack_Valheim-5.4.2333.zip
eternal_fire_file=BenheimEternalFire.dll
eternal_fire_source="$root/server-mods/benheim-eternal-fire/dist/$eternal_fire_file"

download() {
  local url=$1
  local output=$2
  curl -fsSL --retry 3 "$url" -o "$output"
}

download \
  "https://gcdn.thunderstore.io/live/repository/packages/denikson-BepInExPack_Valheim-5.4.2333.zip" \
  "$tmp_dir/$bepinex_file"

if [[ ! -f "$eternal_fire_source" ]]; then
  echo "Missing $eternal_fire_source; run server-mods/benheim-eternal-fire/scripts/build.sh first." >&2
  exit 1
fi
cp "$eternal_fire_source" "$tmp_dir/$eternal_fire_file"

cat > "$tmp_dir/SHA256SUMS" <<EOF
5dd24ccbcaa9260f714b200f23c4c15547e2aa5f06906cafcc0dee56db1bf716  $bepinex_file
8f452cc68d839b7a843676c89b479e357c2b932db8f0f02106de5c5cfde451f4  $eternal_fire_file
EOF

if command -v sha256sum >/dev/null 2>&1; then
  (cd "$tmp_dir" && sha256sum -c SHA256SUMS)
else
  while read -r expected file; do
    actual="$(shasum -a 256 "$tmp_dir/$file" | awk '{print $1}')"
    if [[ "$actual" != "$expected" ]]; then
      echo "Checksum mismatch for $file" >&2
      exit 1
    fi
  done < "$tmp_dir/SHA256SUMS"
fi

remote_ssh "rm -rf /tmp/valheim-server-mods && mkdir -p /tmp/valheim-server-mods"
for file in "$bepinex_file" "$eternal_fire_file" SHA256SUMS; do
  remote_scp "$tmp_dir/$file" "/tmp/valheim-server-mods/$file"
done
remote_scp "$root/server/valheim-start" "/tmp/valheim-server-mods/valheim-start"
remote_scp "$root/server/wait-for-valheim" "/tmp/valheim-server-mods/wait-for-valheim"
remote_scp \
  "$root/server/verify-benheim-eternal-fire" \
  "/tmp/valheim-server-mods/verify-benheim-eternal-fire"
remote_scp \
  "$root/server/recover-valheim-vanilla" \
  "/tmp/valheim-server-mods/recover-valheim-vanilla"

remote_ssh 'bash -s' <<'REMOTE'
set -euo pipefail

work=/tmp/valheim-server-mods
stage="$work/stage"
cd "$work"
sha256sum -c SHA256SUMS

rm -rf "$stage"
mkdir -p "$stage/bepinex"
unzip -q BepInExPack_Valheim-5.4.2333.zip -d "$stage/bepinex"

recover_vanilla() {
  local status=$?
  trap - EXIT
  if [[ $status -ne 0 ]]; then
    echo "Mod installation failed; recovering and proving the vanilla launch path." >&2
    if ! VALHEIM_WAIT_READY="$work/wait-for-valheim" \
      "$work/recover-valheim-vanilla"; then
      echo "Mod installation failed and vanilla recovery did not reach readiness." >&2
      exit 1
    fi
  fi
  exit "$status"
}
trap recover_vanilla EXIT

chmod +x \
  "$work/wait-for-valheim" \
  "$work/verify-benheim-eternal-fire" \
  "$work/recover-valheim-vanilla"

systemctl stop valheim.service
valheim-backup-and-upload

install -m 0755 "$work/valheim-start" /usr/local/bin/valheim-start
install -m 0755 "$work/wait-for-valheim" /usr/local/bin/valheim-wait-ready
cp -a "$stage/bepinex/BepInExPack_Valheim/." /opt/valheim/server/
rm -rf \
  /opt/valheim/server/BepInEx/plugins/Jotunn \
  /opt/valheim/server/BepInEx/plugins/EternalFire
rm -f \
  /opt/valheim/server/BepInEx/config/com.jotunn.jotunn.cfg \
  /opt/valheim/server/BepInEx/config/digitalroot.mods.eternalfire.cfg
install -d /opt/valheim/server/BepInEx/plugins/BenheimEternalFire
install -m 0644 \
  "$work/BenheimEternalFire.dll" \
  /opt/valheim/server/BepInEx/plugins/BenheimEternalFire/BenheimEternalFire.dll

chown -R valheim:valheim /opt/valheim/server/BepInEx /opt/valheim/server/doorstop_libs
if grep -q '^VALHEIM_MODDED=' /etc/valheim/server.env; then
  sed -i 's/^VALHEIM_MODDED=.*/VALHEIM_MODDED=1/' /etc/valheim/server.env
else
  printf '\nVALHEIM_MODDED=1\n' >> /etc/valheim/server.env
fi
started_at="$(date --iso-8601=seconds)"
systemctl start valheim.service
valheim-wait-ready "$started_at"
"$work/verify-benheim-eternal-fire" "$started_at"

trap - EXIT
echo "Installed BepInEx 5.4.2333 and Benheim Eternal Fire 0.1.1."
REMOTE
