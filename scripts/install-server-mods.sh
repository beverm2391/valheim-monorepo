#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

root="$(repo_root)"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

bepinex_file=BepInExPack_Valheim-5.4.2333.zip
jotunn_file=Jotunn-2.29.2.zip
eternal_fire_file=Eternal_Fire-1.0.17.zip

download() {
  local url=$1
  local output=$2
  curl -fsSL --retry 3 "$url" -o "$output"
}

download \
  "https://gcdn.thunderstore.io/live/repository/packages/denikson-BepInExPack_Valheim-5.4.2333.zip" \
  "$tmp_dir/$bepinex_file"
download \
  "https://gcdn.thunderstore.io/live/repository/packages/ValheimModding-Jotunn-2.29.2.zip" \
  "$tmp_dir/$jotunn_file"
download \
  "https://gcdn.thunderstore.io/live/repository/packages/Digitalroot-Eternal_Fire-1.0.17.zip" \
  "$tmp_dir/$eternal_fire_file"

cat > "$tmp_dir/SHA256SUMS" <<EOF
5dd24ccbcaa9260f714b200f23c4c15547e2aa5f06906cafcc0dee56db1bf716  $bepinex_file
c018eb5876ea0b4a509c32b7926055918ea95bc4698610df6b2acc58d72029ad  $jotunn_file
ff5617ca2e0668f0c367ea5fb3cb554fea8fade8875afc2b7050bed3aaf02617  $eternal_fire_file
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
for file in "$bepinex_file" "$jotunn_file" "$eternal_fire_file" SHA256SUMS; do
  remote_scp "$tmp_dir/$file" "/tmp/valheim-server-mods/$file"
done
remote_scp "$root/server/valheim-start" "/tmp/valheim-server-mods/valheim-start"
remote_scp "$root/server/wait-for-valheim" "/tmp/valheim-server-mods/wait-for-valheim"

remote_ssh 'bash -s' <<'REMOTE'
set -euo pipefail

work=/tmp/valheim-server-mods
stage="$work/stage"
cd "$work"
sha256sum -c SHA256SUMS

rm -rf "$stage"
mkdir -p "$stage/bepinex" "$stage/jotunn" "$stage/eternal-fire"
unzip -q BepInExPack_Valheim-5.4.2333.zip -d "$stage/bepinex"
unzip -q Jotunn-2.29.2.zip -d "$stage/jotunn"
unzip -q Eternal_Fire-1.0.17.zip -d "$stage/eternal-fire"

set_modded() {
  local value=$1
  if grep -q '^VALHEIM_MODDED=' /etc/valheim/server.env; then
    sed -i "s/^VALHEIM_MODDED=.*/VALHEIM_MODDED=$value/" /etc/valheim/server.env
  else
    printf '\nVALHEIM_MODDED=%s\n' "$value" >> /etc/valheim/server.env
  fi
}

recover_vanilla() {
  local status=$?
  if [[ $status -ne 0 ]]; then
    echo "Mod installation failed; restarting with the vanilla launch path." >&2
    systemctl stop valheim.service || true
    set_modded 0
    systemctl start valheim.service || true
  fi
  exit "$status"
}
trap recover_vanilla EXIT

systemctl stop valheim.service
valheim-backup-and-upload

install -m 0755 "$work/valheim-start" /usr/local/bin/valheim-start
install -m 0755 "$work/wait-for-valheim" /usr/local/bin/valheim-wait-ready
cp -a "$stage/bepinex/BepInExPack_Valheim/." /opt/valheim/server/
install -d /opt/valheim/server/BepInEx/plugins/Jotunn
cp -a "$stage/jotunn/plugins/." /opt/valheim/server/BepInEx/plugins/Jotunn/
install -d /opt/valheim/server/BepInEx/plugins/EternalFire
install -m 0644 \
  "$stage/eternal-fire/plugins/Digitalroot.Valheim.EternalFire.dll" \
  /opt/valheim/server/BepInEx/plugins/EternalFire/Digitalroot.Valheim.EternalFire.dll
install -m 0644 \
  "$stage/eternal-fire/plugins/LICENSE" \
  /opt/valheim/server/BepInEx/plugins/EternalFire/LICENSE

chown -R valheim:valheim /opt/valheim/server/BepInEx /opt/valheim/server/doorstop_libs
set_modded 1
started_at="$(date --iso-8601=seconds)"
systemctl start valheim.service
valheim-wait-ready "$started_at"

trap - EXIT
echo "Installed BepInEx 5.4.2333, Jotunn 2.29.2, and Eternal Fire 1.0.17."
REMOTE
