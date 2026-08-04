#!/bin/sh
set -eu

game_dir="${BENHEIM_QOL_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
osascript_command="${BENHEIM_OSASCRIPT_COMMAND:-/usr/bin/osascript}"
log_dir="$HOME/Library/Logs/BenheimQoL"
log_file="$log_dir/launch.log"
steam_connection_log="${BENHEIM_STEAM_CONNECTION_LOG:-$HOME/Library/Application Support/Steam/logs/connection_log.txt}"

mkdir -p "$log_dir"

fail() {
  message=$1
  printf '%s\n' "$message" >> "$log_file"
  "$osascript_command" -e "display dialog \"$message\" with title \"Benheim\" buttons {\"OK\"} default button \"OK\"" >/dev/null 2>&1 || true
  exit 1
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

# BepInEx's current macOS Doorstop loader is x86_64, so Apple Silicon Macs run
# Valheim's matching slice under Rosetta. Detach so the Dock app can exit.
nohup arch -x86_64 ./start_game_bepinex.sh ./valheim.app >> "$log_file" 2>&1 &
exit 0
