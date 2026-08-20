#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

root="$(repo_root)"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

bepinex_file=BepInExPack_Valheim-5.4.2333.zip
eternal_fire_file=BenheimEternalFire.dll
test_commands_file=BenheimTestCommands.dll
server_support_file=BenheimServerSupport.dll
eternal_fire_source="$root/server-mods/benheim-eternal-fire/dist/$eternal_fire_file"
test_commands_source="$root/server-mods/benheim-test-commands/dist/$test_commands_file"
server_support_source="$root/server-mods/benheim-server-support/dist/$server_support_file"

bepinex_sha256=5dd24ccbcaa9260f714b200f23c4c15547e2aa5f06906cafcc0dee56db1bf716
eternal_fire_sha256=8f452cc68d839b7a843676c89b479e357c2b932db8f0f02106de5c5cfde451f4
test_commands_sha256=2a15b0714a81ae518ac1d8ab8d0f8e16a87eecc39d4e32ca8b75316b0d051db2
server_support_sha256=cbfcf5b7891c5e3a0a8ebddfb2cfdc19fc708dd54f3a834bcc441bf3ce3e6eca

download() {
  local url=$1
  local output=$2
  curl -fsSL --retry 3 "$url" -o "$output"
}

download \
  "https://gcdn.thunderstore.io/live/repository/packages/denikson-BepInExPack_Valheim-5.4.2333.zip" \
  "$tmp_dir/$bepinex_file"

"$root/server-mods/benheim-eternal-fire/scripts/build.sh"
"$root/server-mods/benheim-test-commands/scripts/build.sh"
"$root/server-mods/benheim-server-support/scripts/build.sh"

cp "$eternal_fire_source" "$tmp_dir/$eternal_fire_file"
cp "$test_commands_source" "$tmp_dir/$test_commands_file"
cp "$server_support_source" "$tmp_dir/$server_support_file"
cat > "$tmp_dir/SHA256SUMS" <<EOF
$bepinex_sha256  $bepinex_file
$eternal_fire_sha256  $eternal_fire_file
$test_commands_sha256  $test_commands_file
$server_support_sha256  $server_support_file
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

remote_ssh "rm -rf /tmp/valheim-server-mods && install -d -m 0700 /tmp/valheim-server-mods"
for file in \
  "$bepinex_file" \
  "$eternal_fire_file" \
  "$test_commands_file" \
  "$server_support_file" \
  SHA256SUMS; do
  remote_scp "$tmp_dir/$file" "/tmp/valheim-server-mods/$file"
done
remote_scp "$root/server/valheim-start" "/tmp/valheim-server-mods/valheim-start"
remote_scp "$root/server/wait-for-valheim" "/tmp/valheim-server-mods/wait-for-valheim"
remote_scp \
  "$root/server/verify-benheim-server-plugins" \
  "/tmp/valheim-server-mods/verify-benheim-server-plugins"
remote_scp \
  "$root/server/recover-valheim-vanilla" \
  "/tmp/valheim-server-mods/recover-valheim-vanilla"

printf -v expected_world_arg '%q' "$VALHEIM_WORLD_NAME"
remote_ssh "bash -s -- $expected_world_arg" <<'REMOTE'
set -euo pipefail

expected_world=${1:?Missing expected world name}
work=/tmp/valheim-server-mods
stage="$work/stage"
cd "$work"
sha256sum -c SHA256SUMS

rm -rf "$stage"
mkdir -p "$stage/bepinex"
unzip -q BepInExPack_Valheim-5.4.2333.zip -d "$stage/bepinex"

recover_previous_state() {
  [[ -f "$work/rollback/system.tar.gz" ]] || return 1
  systemctl stop valheim.service || true
  rm -rf \
    /opt/valheim/server/BepInEx \
    /opt/valheim/server/doorstop_libs
  rm -f \
    /opt/valheim/server/.doorstop_version \
    /opt/valheim/server/changelog.txt \
    /opt/valheim/server/doorstop_config.ini \
    /opt/valheim/server/start_game_bepinex.sh \
    /opt/valheim/server/start_server_bepinex.sh \
    /opt/valheim/server/winhttp.dll \
    /usr/local/bin/valheim-start \
    /usr/local/bin/valheim-wait-ready \
    /etc/valheim/server.env
  tar -xzf "$work/rollback/system.tar.gz" -C /
  local started_at
  started_at="$(date --iso-8601=seconds)"
  systemctl start valheim.service
  if [[ -x /usr/local/bin/valheim-wait-ready ]]; then
    /usr/local/bin/valheim-wait-ready "$started_at"
  else
    "$work/wait-for-valheim" "$started_at"
  fi
}

recover_server() {
  local status=$?
  trap - EXIT
  if [[ $status -ne 0 && ${recovery_armed:-0} -eq 1 ]]; then
    echo "Mod installation failed; restoring the exact previous server state." >&2
    if ! recover_previous_state; then
      echo "Previous-state recovery failed; proving the vanilla launch path." >&2
      if ! VALHEIM_WAIT_READY="$work/wait-for-valheim" \
        "$work/recover-valheim-vanilla"; then
        echo "Mod installation failed and neither recovery path reached readiness." >&2
        status=1
      fi
    fi
  fi
  rm -rf "$work"
  exit "$status"
}
trap recover_server EXIT
recovery_armed=0

chmod +x \
  "$work/wait-for-valheim" \
  "$work/verify-benheim-server-plugins" \
  "$work/recover-valheim-vanilla"

rm -rf "$work/rollback"
mkdir -p "$work/rollback"
snapshot_paths=()
for path in \
  /opt/valheim/server/BepInEx \
  /opt/valheim/server/doorstop_libs \
  /opt/valheim/server/.doorstop_version \
  /opt/valheim/server/changelog.txt \
  /opt/valheim/server/doorstop_config.ini \
  /opt/valheim/server/start_game_bepinex.sh \
  /opt/valheim/server/start_server_bepinex.sh \
  /opt/valheim/server/winhttp.dll \
  /usr/local/bin/valheim-start \
  /usr/local/bin/valheim-wait-ready \
  /etc/valheim/server.env; do
  if [[ -e "$path" || -L "$path" ]]; then
    snapshot_paths+=("${path#/}")
  fi
done
tar -czf "$work/rollback/system.tar.gz.tmp" -C / "${snapshot_paths[@]}"
chmod 0600 "$work/rollback/system.tar.gz.tmp"
tar -tzf "$work/rollback/system.tar.gz.tmp" >/dev/null
mv "$work/rollback/system.tar.gz.tmp" "$work/rollback/system.tar.gz"
recovery_armed=1

systemctl stop valheim.service
valheim-backup-and-upload

install -m 0755 "$work/valheim-start" /usr/local/bin/valheim-start
install -m 0755 "$work/wait-for-valheim" /usr/local/bin/valheim-wait-ready
cp -a "$stage/bepinex/BepInExPack_Valheim/." /opt/valheim/server/
rm -rf /opt/valheim/server/BepInEx/plugins
rm -f \
  /opt/valheim/server/BepInEx/config/com.jotunn.jotunn.cfg \
  /opt/valheim/server/BepInEx/config/digitalroot.mods.eternalfire.cfg
install -d \
  /opt/valheim/server/BepInEx/plugins/BenheimEternalFire \
  /opt/valheim/server/BepInEx/plugins/BenheimTestCommands \
  /opt/valheim/server/BepInEx/plugins/BenheimServerSupport
install -m 0644 \
  "$work/BenheimEternalFire.dll" \
  /opt/valheim/server/BepInEx/plugins/BenheimEternalFire/BenheimEternalFire.dll
install -m 0644 \
  "$work/BenheimTestCommands.dll" \
  /opt/valheim/server/BepInEx/plugins/BenheimTestCommands/BenheimTestCommands.dll
install -m 0644 \
  "$work/BenheimServerSupport.dll" \
  /opt/valheim/server/BepInEx/plugins/BenheimServerSupport/BenheimServerSupport.dll
cmp -s \
  "$work/BenheimEternalFire.dll" \
  /opt/valheim/server/BepInEx/plugins/BenheimEternalFire/BenheimEternalFire.dll
cmp -s \
  "$work/BenheimTestCommands.dll" \
  /opt/valheim/server/BepInEx/plugins/BenheimTestCommands/BenheimTestCommands.dll
cmp -s \
  "$work/BenheimServerSupport.dll" \
  /opt/valheim/server/BepInEx/plugins/BenheimServerSupport/BenheimServerSupport.dll
chown -R valheim:valheim /opt/valheim/server/BepInEx /opt/valheim/server/doorstop_libs
if grep -q '^VALHEIM_MODDED=' /etc/valheim/server.env; then
  sed -i 's/^VALHEIM_MODDED=.*/VALHEIM_MODDED=1/' /etc/valheim/server.env
else
  printf '\nVALHEIM_MODDED=1\n' >> /etc/valheim/server.env
fi
systemctl start valheim.service
invocation_id="$(systemctl show --property=InvocationID --value valheim.service)"
if [[ -z "$invocation_id" ]]; then
  echo "Valheim started without a systemd invocation ID." >&2
  exit 1
fi
"$work/verify-benheim-server-plugins" "$invocation_id" "$expected_world"

trap - EXIT
rm -rf "$work"
echo "Installed BepInEx 5.4.2333, Benheim Eternal Fire 0.1.1, Benheim Test Commands 0.1.1, and Benheim Server Support 0.1.4."
REMOTE
