#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

game_dir="$test_root/Valheim"
mock_bin="$test_root/bin"
launch_log="$test_root/launch.log"
steam_connection_log="$test_root/connection_log.txt"
bepinex_log="$game_dir/BepInEx/LogOutput.log"
structured_events="$game_dir/BepInEx/BenheimEvents.ndjson"
archive_dir="$game_dir/BepInEx/BenheimLogArchive"
mkdir -p "$game_dir/valheim.app" "$game_dir/BepInEx" "$mock_bin"
cat > "$game_dir/start_game_bepinex.sh" <<'SH'
#!/bin/sh
printf 'new BepInEx session\n' > "$BENHEIM_TEST_BEPINEX_LOG"
printf '{"event":"new_session"}\n' > "$BENHEIM_TEST_EVENT_LOG"
SH
printf '%s\n' '[Logged On, 4, 7] RecvMsgClientLogOnResponse() : processing complete' > "$steam_connection_log"
chmod +x "$game_dir/start_game_bepinex.sh"

cat > "$mock_bin/pgrep" <<'EOF'
#!/bin/sh
exit 0
EOF
cat > "$mock_bin/nohup" <<'EOF'
#!/bin/sh
printf '%s\n' "$*" >> "$BENHEIM_TEST_LAUNCH_LOG"
if [ "$1" = 'arch' ]; then
  shift 2
fi
"$@"
EOF
cat > "$mock_bin/osascript" <<'EOF'
#!/bin/sh
printf '%s\n' "$*" >> "$BENHEIM_TEST_OSASCRIPT_LOG"
EOF
chmod +x "$mock_bin/pgrep" "$mock_bin/nohup" "$mock_bin/osascript"

run_launcher() {
  HOME="$test_root/home" \
  PATH="$mock_bin:$PATH" \
  BENHEIM_QOL_GAME_DIR="$game_dir" \
  BENHEIM_OSASCRIPT_COMMAND="$mock_bin/osascript" \
  BENHEIM_STEAM_CONNECTION_LOG="$steam_connection_log" \
  BENHEIM_TEST_LAUNCH_LOG="$launch_log" \
  BENHEIM_TEST_BEPINEX_LOG="$bepinex_log" \
  BENHEIM_TEST_EVENT_LOG="$structured_events" \
  BENHEIM_TEST_OSASCRIPT_LOG="$test_root/osascript.log" \
    "$root/scripts/macos-launcher.sh"
}

archive_count() {
  count=0
  for archive in "$archive_dir"/Benheim-session-*.log; do
    [ -f "$archive" ] || continue
    count=$((count + 1))
  done
  printf '%s\n' "$count"
}

event_archive_count() {
  count=0
  for archive in "$archive_dir"/Benheim-session-*.ndjson; do
    [ -f "$archive" ] || continue
    count=$((count + 1))
  done
  printf '%s\n' "$count"
}

archive_contains() {
  expected_file="$1"
  for archive in "$archive_dir"/Benheim-session-*.log; do
    [ -f "$archive" ] || continue
    if cmp -s "$expected_file" "$archive"; then
      return 0
    fi
  done
  return 1
}

printf 'new BepInEx session\n' > "$test_root/current-after-launch.log"

wait_for_new_session() {
  attempts=0
  while [ "$attempts" -lt 100 ]; do
    if cmp -s "$test_root/current-after-launch.log" "$bepinex_log"; then
      return 0
    fi
    sleep 0.05
    attempts=$((attempts + 1))
  done
  echo "the fake BepInEx launch did not replace LogOutput.log" >&2
  test ! -f "$launch_log" || sed -n '1,80p' "$launch_log" >&2
  return 1
}

# Eleven completed launches leave exactly the ten newest archived sessions;
# the current BepInEx log remains untouched until the next BepInEx process.
i=0
while [ "$i" -lt 11 ]; do
  expected="$test_root/session-$i.log"
  printf 'completed session %s\n' "$i" > "$expected"
  printf 'completed session %s\n' "$i" > "$bepinex_log"
  printf '{"session":%s}\n' "$i" > "$structured_events"
  run_launcher
  wait_for_new_session
  i=$((i + 1))
done
test "$(archive_count)" = 10
test "$(event_archive_count)" = 10
if archive_contains "$test_root/session-0.log"; then
  echo "the oldest Benheim session archive was not pruned" >&2
  exit 1
fi
archive_contains "$test_root/session-1.log"
archive_contains "$test_root/session-10.log"
for archive in "$archive_dir"/Benheim-session-*.log; do
  test -f "${archive%.log}.ndjson"
done
cmp -s "$test_root/current-after-launch.log" "$bepinex_log"

# A pre-structured session still gets an empty paired event archive. Orphaned
# event files do not escape the same ten-session retention boundary.
printf 'legacy text-only session\n' > "$bepinex_log"
rm -f "$structured_events"
printf '{"orphan":true}\n' > "$archive_dir/Benheim-session-20000101T000000Z-001.ndjson"
run_launcher
wait_for_new_session
for archive in "$archive_dir"/Benheim-session-*.log; do
  test -f "${archive%.log}.ndjson"
done
test ! -f "$archive_dir/Benheim-session-20000101T000000Z-001.ndjson"

# A crash leaves its log in place; the next managed launch archives it before
# BepInEx can replace LogOutput.log.
printf 'crashed session\n' > "$test_root/crash.log"
printf 'crashed session\n' > "$bepinex_log"
printf '{"session":"crashed"}\n' > "$structured_events"
run_launcher
wait_for_new_session
test "$(archive_count)" = 10
archive_contains "$test_root/crash.log"
printf 'next session\n' > "$test_root/next.log"
printf 'next session\n' > "$bepinex_log"
cmp -s "$test_root/next.log" "$bepinex_log"

# Orphan cleanup is independent of text-log archival. A structured archive
# left by an interrupted cleanup is removed even when no text archives or
# current BepInEx log exist.
rm -f "$archive_dir"/Benheim-session-*.log "$archive_dir"/Benheim-session-*.ndjson
printf '{"orphan":true}\n' > "$archive_dir/Benheim-session-20000101T000000Z-002.ndjson"
rm -f "$bepinex_log"
run_launcher
wait_for_new_session
test ! -f "$archive_dir/Benheim-session-20000101T000000Z-002.ndjson"

# Pruning is scoped to Benheim's name pattern; unrelated files survive.
printf 'unrelated archive\n' > "$archive_dir/player-notes.log"
test -f "$archive_dir/player-notes.log"

# An archive write failure is visible as a native notification and launcher
# warning, but does not block the modded launch.
real_cp="$(command -v cp)"
cat > "$mock_bin/cp" <<SH
#!/bin/sh
case "\$2" in
  *.ndjson) printf 'partial archive\n' > "\$2"; exit 42 ;;
esac
exec "$real_cp" "\$@"
SH
chmod +x "$mock_bin/cp"
printf 'archive failure session\n' > "$bepinex_log"
before_failure_count="$(archive_count)"
before_failure_event_count="$(event_archive_count)"
run_launcher
wait_for_new_session
grep -Fq 'Warning: Could not archive the previous Benheim session; continuing launch.' "$test_root/home/Library/Logs/BenheimQoL/launch.log"
grep -Fq 'display notification' "$test_root/osascript.log"
test "$(archive_count)" = "$before_failure_count"
test "$(event_archive_count)" = "$before_failure_event_count"
test -f "$launch_log"

# Windows has no PowerShell runtime in this macOS test environment; verify its
# archive behavior and both installer/package regeneration paths structurally.
windows_launcher="$root/scripts/launch-windows.ps1"
windows_installer="$root/scripts/install-windows.ps1"
grep -Fq 'function Archive-PreviousSession' "$windows_launcher"
grep -Fq 'BepInEx\LogOutput.log' "$windows_launcher"
grep -Fq 'BepInEx\BenheimEvents.ndjson' "$windows_launcher"
grep -Fq 'BepInEx\BenheimLogArchive' "$windows_launcher"
grep -Fq "Benheim-session-*.log" "$windows_launcher"
grep -Fq 'Archive-PreviousSession -GameDir $gameDir' "$windows_launcher"
grep -Fq 'msg.exe' "$windows_launcher"
grep -Fq 'foreach ($existingArchive in @(' "$windows_launcher"
grep -Fq '$existingNumber = [int]$Matches' "$windows_launcher"
grep -Fq '$index = $existingNumber + 1' "$windows_launcher"
grep -Fq '$archivePath = $null' "$windows_launcher"
grep -Fq 'Remove-Item -LiteralPath $archivePath -Force -ErrorAction SilentlyContinue' "$windows_launcher"
grep -Fq "[IO.Path]::ChangeExtension(\$archivePath, '.ndjson')" "$windows_launcher"
mac_archive_line="$(grep -nF 'archive_previous_session' "$root/scripts/macos-launcher.sh" | tail -n 1 | cut -d: -f1)"
mac_launch_line="$(grep -nF 'nohup arch -x86_64' "$root/scripts/macos-launcher.sh" | cut -d: -f1)"
test "$mac_archive_line" -lt "$mac_launch_line"
grep -Fq 'Copy-Item -LiteralPath $LauncherSource -Destination (Join-Path $stagedLauncherRoot' "$windows_installer"
grep -Fq 'install -m 0755 "$launcher_source"' "$root/scripts/install-macos.command"

# Both shareable packages must regenerate from the current managed launchers.
printf 'test-dll\n' > "$test_root/BenheimQoL.dll"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
mac_dist="$test_root/mac-dist"
windows_dist="$test_root/windows-dist"
BENHEIM_QOL_DLL="$test_root/BenheimQoL.dll" \
BENHEIM_QOL_DIST="$mac_dist" \
BENHEIM_QOL_SKIP_BUILD=1 \
  "$root/scripts/package-macos.sh" >/dev/null
BENHEIM_QOL_DLL="$test_root/BenheimQoL.dll" \
BENHEIM_QOL_DIST="$windows_dist" \
BENHEIM_QOL_SKIP_BUILD=1 \
  "$root/scripts/package-windows.sh" >/dev/null
unzip -qq "$mac_dist/Benheim-macOS-$version.zip" -d "$test_root/mac-extracted"
unzip -qq "$windows_dist/Benheim-Windows-$version.zip" -d "$test_root/windows-extracted"
cmp -s \
  "$root/scripts/macos-launcher.sh" \
  "$test_root/mac-extracted/Benheim-macOS-$version/macos-launcher.sh"
cmp -s \
  "$root/scripts/launch-windows.ps1" \
  "$test_root/windows-extracted/Benheim-Windows-$version/launch-windows.ps1"

echo "Benheim cross-platform session log retention and package regeneration checks passed"
