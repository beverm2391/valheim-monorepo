#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

game_dir="$test_root/Valheim"
mock_bin="$test_root/bin"
responses="$test_root/responses"
updater_log="$test_root/updater.log"
launch_log="$test_root/launch.log"
mkdir -p "$game_dir/valheim.app" "$game_dir/BepInEx/plugins/BenheimQoL" "$mock_bin"
printf '#!/bin/sh\nexit 0\n' > "$game_dir/start_game_bepinex.sh"
chmod +x "$game_dir/start_game_bepinex.sh"

cat > "$mock_bin/pgrep" <<'EOF'
#!/bin/sh
exit 0
EOF
cat > "$mock_bin/nohup" <<'EOF'
#!/bin/sh
printf 'launched\n' >> "$BENHEIM_TEST_LAUNCH_LOG"
EOF
cat > "$mock_bin/osascript" <<'EOF'
#!/bin/sh
response="$(head -n 1 "$BENHEIM_TEST_RESPONSES")"
tail -n +2 "$BENHEIM_TEST_RESPONSES" > "$BENHEIM_TEST_RESPONSES.next"
mv "$BENHEIM_TEST_RESPONSES.next" "$BENHEIM_TEST_RESPONSES"
printf '%s\n' "$response"
EOF
cat > "$test_root/updater" <<'EOF'
#!/bin/sh
printf 'updated\n' >> "$BENHEIM_TEST_UPDATER_LOG"
exit "${BENHEIM_TEST_UPDATE_EXIT:-0}"
EOF
chmod +x "$mock_bin/pgrep" "$mock_bin/nohup" "$mock_bin/osascript" "$test_root/updater"

run_launcher() {
  HOME="$test_root/home" \
  PATH="$mock_bin:$PATH" \
  BENHEIM_QOL_GAME_DIR="$game_dir" \
  BENHEIM_UPDATE_VERSION_URL="file://$test_root/latest-version" \
  BENHEIM_UPDATE_COMMAND="$test_root/updater" \
  BENHEIM_OSASCRIPT_COMMAND="$mock_bin/osascript" \
  BENHEIM_TEST_RESPONSES="$responses" \
  BENHEIM_TEST_UPDATER_LOG="$updater_log" \
  BENHEIM_TEST_LAUNCH_LOG="$launch_log" \
    "$root/scripts/macos-launcher.sh"
}

wait_for_launch() {
  for _ in {1..20}; do
    if [[ -f "$launch_log" ]]; then
      return
    fi
    sleep 0.05
  done
  echo "Benheim did not start" >&2
  exit 1
}

printf '0.1.35\n' > "$game_dir/BepInEx/plugins/BenheimQoL/VERSION"
printf '0.1.36\n' > "$test_root/latest-version"
printf 'Update and launch\n' > "$responses"
run_launcher
wait_for_launch
grep -Fqx 'updated' "$updater_log"
grep -Fqx 'launched' "$launch_log"

rm -f "$updater_log" "$launch_log"
printf 'Launch current version\n' > "$responses"
run_launcher
wait_for_launch
test ! -e "$updater_log"
grep -Fqx 'launched' "$launch_log"

rm -f "$updater_log" "$launch_log"
printf 'Update and launch\nLaunch current version\n' > "$responses"
BENHEIM_TEST_UPDATE_EXIT=1 run_launcher
wait_for_launch
grep -Fqx 'updated' "$updater_log"
grep -Fqx 'launched' "$launch_log"

rm -f "$updater_log" "$launch_log"
printf '0.1.35\n' > "$test_root/latest-version"
: > "$responses"
run_launcher
wait_for_launch
test ! -e "$updater_log"
grep -Fqx 'launched' "$launch_log"

rm -f "$updater_log" "$launch_log" "$test_root/latest-version"
run_launcher
wait_for_launch
test ! -e "$updater_log"
grep -Fqx 'launched' "$launch_log"

echo "macOS prompted launcher checks passed"
