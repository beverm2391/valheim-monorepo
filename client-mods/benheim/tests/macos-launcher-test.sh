#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

game_dir="$test_root/Valheim"
mock_bin="$test_root/bin"
launch_log="$test_root/launch.log"
steam_connection_log="$test_root/connection_log.txt"
mkdir -p "$game_dir/valheim.app" "$mock_bin"
printf '#!/bin/sh\nexit 0\n' > "$game_dir/start_game_bepinex.sh"
printf '%s\n' '[Logged On, 4, 7] RecvMsgClientLogOnResponse() : processing complete' > "$steam_connection_log"
chmod +x "$game_dir/start_game_bepinex.sh"

cat > "$mock_bin/pgrep" <<'EOF'
#!/bin/sh
exit 0
EOF
cat > "$mock_bin/nohup" <<'EOF'
#!/bin/sh
printf '%s\n' "$*" > "$BENHEIM_TEST_LAUNCH_LOG"
EOF
chmod +x "$mock_bin/pgrep" "$mock_bin/nohup"

HOME="$test_root/home" \
PATH="$mock_bin:$PATH" \
BENHEIM_QOL_GAME_DIR="$game_dir" \
BENHEIM_STEAM_CONNECTION_LOG="$steam_connection_log" \
BENHEIM_TEST_LAUNCH_LOG="$launch_log" \
  "$root/scripts/macos-launcher.sh"

for _ in {1..20}; do
  [[ -f "$launch_log" ]] && break
  sleep 0.05
done

grep -Fq 'arch -x86_64 ./start_game_bepinex.sh ./valheim.app' "$launch_log"
grep -Fq 'open -a Steam' "$root/scripts/macos-launcher.sh"
grep -Fq 'processing complete' "$root/scripts/macos-launcher.sh"
grep -Fq 'steam_logged_on' "$root/scripts/macos-launcher.sh"
! grep -Fq 'pgrep -x ipcserver' "$root/scripts/macos-launcher.sh"
! grep -Eq 'curl|github|BENHEIM_UPDATE|Update and launch|Launch current version' "$root/scripts/macos-launcher.sh"

# Native executable appears while Steam is starting. The final Lab guard must
# refuse launch and leave the session log alone rather than archive it.
rm "$launch_log"
printf '%s\n' 'current occupied session' > "$game_dir/BepInEx-log-placeholder"
mkdir -p "$game_dir/BepInEx"
cp "$game_dir/BepInEx-log-placeholder" "$game_dir/BepInEx/LogOutput.log"
printf '%s\n' '[Logged Off, 4, 7]' > "$steam_connection_log"
cat > "$mock_bin/pgrep" <<'EOF'
#!/bin/sh
case "$2" in
  steam_osx) exit 0 ;;
  Valheim)
    if [ -e "$BENHEIM_TEST_OCCUPIED" ]; then printf '4242\n'; exit 0; fi
    exit 1 ;;
  *) exit 1 ;;
esac
EOF
cat > "$mock_bin/open" <<'EOF'
#!/bin/sh
touch "$BENHEIM_TEST_OCCUPIED"
printf '%s\n' '[Logged On, 4, 7] RecvMsgClientLogOnResponse() : processing complete' > "$BENHEIM_STEAM_CONNECTION_LOG"
EOF
cat > "$mock_bin/sleep" <<'EOF'
#!/bin/sh
exit 0
EOF
chmod +x "$mock_bin/open" "$mock_bin/sleep"
if HOME="$test_root/home" \
PATH="$mock_bin:$PATH" \
BENHEIM_QOL_GAME_DIR="$game_dir" \
BENHEIM_STEAM_CONNECTION_LOG="$steam_connection_log" \
BENHEIM_TEST_OCCUPIED="$test_root/occupied" \
BENHEIM_TEST_LAUNCH_LOG="$launch_log" \
BENHEIM_OSASCRIPT_COMMAND=/usr/bin/true \
VALHEIM_DEV_LAUNCH_ID=test-owned-launch \
  "$root/scripts/macos-launcher.sh"; then
  echo "Lab launcher accepted a game started during Steam readiness" >&2
  exit 1
fi
test ! -e "$launch_log"
test ! -d "$game_dir/BepInEx/BenheimLogArchive"
cmp "$game_dir/BepInEx-log-placeholder" "$game_dir/BepInEx/LogOutput.log"

echo "macOS direct modded-launch checks passed"
