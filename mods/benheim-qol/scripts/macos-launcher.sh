#!/bin/sh
set -eu

game_dir="${BENHEIM_QOL_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
plugin_dir="$game_dir/BepInEx/plugins/BenheimQoL"
installed_version_file="$plugin_dir/VERSION"
latest_version_url="${BENHEIM_UPDATE_VERSION_URL:-https://github.com/beverm2391/valheim-server/releases/latest/download/VERSION}"
updater="${BENHEIM_UPDATE_COMMAND:-$HOME/Applications/Update Benheim.app/Contents/MacOS/UpdateBenheim}"
osascript_command="${BENHEIM_OSASCRIPT_COMMAND:-/usr/bin/osascript}"
log_dir="$HOME/Library/Logs/BenheimQoL"
log_file="$log_dir/launch.log"

mkdir -p "$log_dir"

fail() {
  message=$1
  printf '%s\n' "$message" >> "$log_file"
  "$osascript_command" -e "display dialog \"$message\" with title \"Benheim\" buttons {\"OK\"} default button \"OK\"" >/dev/null 2>&1 || true
  exit 1
}

is_semver() {
  printf '%s\n' "$1" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$'
}

is_newer_version() {
  awk -v latest="$1" -v installed="$2" 'BEGIN {
    split(latest, l, ".")
    split(installed, i, ".")
    for (part = 1; part <= 3; part++) {
      if ((l[part] + 0) > (i[part] + 0)) exit 0
      if ((l[part] + 0) < (i[part] + 0)) exit 1
    }
    exit 1
  }'
}

prompt_for_update() {
  "$osascript_command" - "$1" "$2" <<'APPLESCRIPT' 2>/dev/null
on run argv
  set installedVersion to item 1 of argv
  set latestVersion to item 2 of argv
  set promptText to "Benheim " & latestVersion & " is available. You have " & installedVersion & "."
  display dialog promptText with title "Benheim update available" buttons {"Launch current version", "Update and launch"} default button "Update and launch"
  return button returned of result
end run
APPLESCRIPT
}

prompt_after_update_failure() {
  "$osascript_command" <<'APPLESCRIPT' 2>/dev/null
set promptText to "The update could not finish. Your current Benheim installation was not changed."
display dialog promptText with title "Benheim update failed" buttons {"Cancel", "Launch current version"} default button "Launch current version" cancel button "Cancel"
return button returned of result
APPLESCRIPT
}

if [ ! -x "$game_dir/start_game_bepinex.sh" ] || [ ! -d "$game_dir/valheim.app" ]; then
  fail "Benheim is not installed correctly. Run the Mac installer again."
fi

printf '%s\n' "Launching Benheim..." > "$log_file"

if [ -f "$installed_version_file" ]; then
  installed_version="$(tr -d '[:space:]' < "$installed_version_file")"
  latest_version="$(curl -fsSL --connect-timeout 2 --max-time 4 "$latest_version_url" 2>/dev/null | tr -d '[:space:]' || true)"

  if is_semver "$installed_version" && is_semver "$latest_version" \
    && is_newer_version "$latest_version" "$installed_version"; then
    choice="$(prompt_for_update "$installed_version" "$latest_version" || true)"
    if [ "$choice" = "Update and launch" ]; then
      if [ ! -x "$updater" ]; then
        fail "The Benheim updater is missing. Run the Mac installer again."
      fi

      printf '%s\n' "Updating Benheim $installed_version to $latest_version..." >> "$log_file"
      if ! BENHEIM_UPDATE_NO_UI=1 "$updater" >> "$log_file" 2>&1; then
        choice="$(prompt_after_update_failure || true)"
        if [ "$choice" != "Launch current version" ]; then
          exit 0
        fi
      fi
    fi
  fi
fi

if ! pgrep -x steam_osx >/dev/null 2>&1 || ! pgrep -x ipcserver >/dev/null 2>&1; then
  printf '%s\n' "Starting Steam..." >> "$log_file"
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
