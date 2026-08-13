#!/bin/sh
set -eu

game_dir="${BENHEIM_QOL_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
osascript_command="${BENHEIM_OSASCRIPT_COMMAND:-/usr/bin/osascript}"
log_dir="$HOME/Library/Logs/BenheimQoL"
log_file="$log_dir/launch.log"
steam_connection_log="${BENHEIM_STEAM_CONNECTION_LOG:-$HOME/Library/Application Support/Steam/logs/connection_log.txt}"
bepinex_log_file="$game_dir/BepInEx/LogOutput.log"
structured_event_file="$game_dir/BepInEx/BenheimEvents.ndjson"
archive_dir="$game_dir/BepInEx/BenheimLogArchive"
archive_prefix='Benheim-session-'

mkdir -p "$log_dir"

fail() {
  message=$1
  printf '%s\n' "$message" >> "$log_file"
  "$osascript_command" -e "display dialog \"$message\" with title \"Benheim\" buttons {\"OK\"} default button \"OK\"" >/dev/null 2>&1 || true
  exit 1
}

warn() {
  message=$1
  printf '%s\n' "Warning: $message" >> "$log_file" 2>/dev/null || true
  "$osascript_command" -e "display notification \"$message\" with title \"Benheim\"" >/dev/null 2>&1 || true
}

prune_archives() {
  if ! candidates="$({
    for candidate in "$archive_dir"/$archive_prefix*.log; do
      [ -f "$candidate" ] && printf '%s\n' "$candidate"
    done
  } | LC_ALL=C sort -r)"; then
    warn "Could not inspect Benheim session archives; continuing launch."
    return 0
  fi

  if [ -n "$candidates" ]; then
    kept=0
    while IFS= read -r candidate; do
      [ -n "$candidate" ] || continue
      kept=$((kept + 1))
      if [ "$kept" -gt 10 ] && ! rm -f "$candidate"; then
        warn "Could not prune a Benheim session archive; continuing launch."
      elif [ "$kept" -gt 10 ]; then
        rm -f "${candidate%.log}.ndjson" 2>/dev/null || \
          warn "Could not prune a Benheim structured event archive; continuing launch."
      fi
    done <<EOF
$candidates
EOF
  fi

  for event_archive in "$archive_dir"/$archive_prefix*.ndjson; do
    [ -f "$event_archive" ] || continue
    [ -f "${event_archive%.ndjson}.log" ] || rm -f "$event_archive" 2>/dev/null || \
      warn "Could not prune an orphaned Benheim structured event archive; continuing launch."
  done
}

archive_previous_session() {
  if [ ! -f "$bepinex_log_file" ]; then
    prune_archives
    return 0
  fi

  if ! mkdir -p "$archive_dir"; then
    warn "Could not create the Benheim session archive directory; continuing launch."
    return 0
  fi

  session_stamp="$(date -u '+%Y%m%dT%H%M%SZ' 2>/dev/null || printf 'unknown')"
  archive_index=1
  for candidate in "$archive_dir/$archive_prefix$session_stamp-"*.log; do
    [ -f "$candidate" ] || continue
    archive_suffix="${candidate##*-}"
    archive_suffix="${archive_suffix%.log}"
    case "$archive_suffix" in
      ''|*[!0-9]*) continue ;;
    esac
    archive_number="$(printf '%s\n' "$archive_suffix" | awk '{sub(/^0+/, "", $0); print ($0 == "" ? 0 : $0)}')"
    if [ "$archive_number" -ge "$archive_index" ]; then
      archive_index=$((archive_number + 1))
    fi
  done
  while :; do
    archive_suffix="$(printf '%03d' "$archive_index")"
    archive_path="$archive_dir/$archive_prefix$session_stamp-$archive_suffix.log"
    [ ! -e "$archive_path" ] && break
    archive_index=$((archive_index + 1))
  done

  if ! cp "$bepinex_log_file" "$archive_path"; then
    rm -f "$archive_path" 2>/dev/null || true
    warn "Could not archive the previous BepInEx log; continuing launch."
    return 0
  fi

  event_archive_path="${archive_path%.log}.ndjson"
  if [ -f "$structured_event_file" ]; then
    event_source="$structured_event_file"
  else
    event_source=/dev/null
  fi
  if ! cp "$event_source" "$event_archive_path"; then
    rm -f "$archive_path" "$event_archive_path" 2>/dev/null || true
    warn "Could not archive the previous Benheim session; continuing launch."
    return 0
  fi

  prune_archives
}

if [ ! -x "$game_dir/start_game_bepinex.sh" ] || [ ! -d "$game_dir/valheim.app" ]; then
  fail "Benheim is not installed correctly. Run the Mac installer again."
fi

printf '%s\n' "Launching Benheim..." > "$log_file"

steam_logged_on() {
  [ -f "$steam_connection_log" ] || return 1

  # ipcserver can survive after Steam exits, so process existence is not a
  # readiness signal. The connection log records the authoritative login state.
  tail -n 500 "$steam_connection_log" | awk '
    /\[Logged Off,/ { logged_on = 0 }
    /\[Logged On,/ && /processing complete/ { logged_on = 1 }
    END { exit logged_on == 1 ? 0 : 1 }
  '
}

if ! pgrep -x steam_osx >/dev/null 2>&1 || ! steam_logged_on; then
  printf '%s\n' "Starting Steam..." >> "$log_file"
  open -a Steam

  waited=0
  while [ "$waited" -lt 90 ]; do
    if pgrep -x steam_osx >/dev/null 2>&1 && steam_logged_on; then
      break
    fi

    sleep 1
    waited=$((waited + 1))
  done

  if ! pgrep -x steam_osx >/dev/null 2>&1 || ! steam_logged_on; then
    fail "Steam did not become ready. Open Steam, sign in, and try Benheim again."
  fi

  # Let the freshly authenticated client publish its global-user IPC state.
  sleep 1
fi

cd "$game_dir"

# Archive only after Steam is ready, at the last safe point before BepInEx can
# overwrite LogOutput.log. A failed Steam preflight therefore does not create a
# duplicate archive on the next attempt.
archive_previous_session

# BepInEx's current macOS Doorstop loader is x86_64, so Apple Silicon Macs run
# Valheim's matching slice under Rosetta. Detach so the Dock app can exit.
nohup arch -x86_64 ./start_game_bepinex.sh ./valheim.app >> "$log_file" 2>&1 &
exit 0
