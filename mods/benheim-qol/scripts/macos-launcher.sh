#!/bin/sh
set -eu

game_dir="${BENHEIM_QOL_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
log_dir="$HOME/Library/Logs/BenheimQoL"
log_file="$log_dir/launch.log"

mkdir -p "$log_dir"

fail() {
  message=$1
  printf '%s\n' "$message" >> "$log_file"
  osascript -e "display dialog \"$message\" with title \"Benheim\" buttons {\"OK\"} default button \"OK\"" >/dev/null 2>&1 || true
  exit 1
}

if [ ! -x "$game_dir/start_game_bepinex.sh" ] || [ ! -d "$game_dir/valheim.app" ]; then
  fail "Benheim is not installed correctly. Run the Mac installer again."
fi

if ! pgrep -x steam_osx >/dev/null 2>&1 || ! pgrep -x ipcserver >/dev/null 2>&1; then
  printf '%s\n' "Starting Steam..." > "$log_file"
  open -a Steam

  waited=0
  while [ "$waited" -lt 90 ]; do
    if pgrep -x steam_osx >/dev/null 2>&1 && pgrep -x ipcserver >/dev/null 2>&1; then
      break
    fi

    sleep 1
    waited=$((waited + 1))
  done

  if ! pgrep -x steam_osx >/dev/null 2>&1 || ! pgrep -x ipcserver >/dev/null 2>&1; then
    fail "Steam did not become ready. Open Steam, sign in, and try Benheim again."
  fi

  # Steam's IPC process can appear just before the client finishes accepting
  # game launches. This short grace period keeps a cold launch deterministic.
  sleep 3
fi

cd "$game_dir"

# BepInEx's current macOS Doorstop loader is x86_64, so Apple Silicon Macs run
# Valheim's matching slice under Rosetta. Detach so the Dock app can exit.
nohup arch -x86_64 ./start_game_bepinex.sh ./valheim.app >> "$log_file" 2>&1 &
exit 0
