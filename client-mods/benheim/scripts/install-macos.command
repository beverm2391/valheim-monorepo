#!/bin/bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
game_dir="${BENHEIM_QOL_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
app_parent="${BENHEIM_QOL_APP_DIR:-$HOME/Applications}"
app="$app_parent/Benheim.app"
updater_app="$app_parent/Update Benheim.app"
legacy_app="$app_parent/Benheim QoL.app"
plugin_dir="$game_dir/BepInEx/plugins/BenheimQoL"
plugin="$plugin_dir/BenheimQoL.dll"
dll="${BENHEIM_QOL_DLL:-$script_dir/BenheimQoL.dll}"
launcher_source="${BENHEIM_QOL_LAUNCHER_SOURCE:-$script_dir/macos-launcher.sh}"
version_source="${BENHEIM_QOL_VERSION_FILE:-$script_dir/VERSION}"
private_diagnostics_source="${BENHEIM_QOL_PRIVATE_DIAGNOSTICS_FILE:-$script_dir/PRIVATE-TEST-DIAGNOSTICS.cfg}"
private_diagnostics="$game_dir/BepInEx/config/BenheimPrivateDiagnostics.cfg"
bepinex_url="${BENHEIM_QOL_BEPINEX_URL:-https://gcdn.thunderstore.io/live/repository/packages/denikson-BepInExPack_Valheim-5.4.2333.zip}"
if [[ -n "${BENHEIM_QOL_PRIVATE_DIAGNOSTICS_FILE:-}" ]]; then
  private_diagnostics_source="$BENHEIM_QOL_PRIVATE_DIAGNOSTICS_FILE"
fi
bepinex_sha256="${BENHEIM_QOL_BEPINEX_SHA256:-5dd24ccbcaa9260f714b200f23c4c15547e2aa5f06906cafcc0dee56db1bf716}"
tmp_dir="$(mktemp -d)"
staged_app=""
backup_app=""
backup_updater_app=""
app_installed=0
updater_app_owned=0
plugin_replaced=0
plugin_had_previous=0
plugin_backup="$tmp_dir/BenheimQoL.previous.dll"
installed_version="$plugin_dir/VERSION"
version_backup="$tmp_dir/VERSION.previous"
version_replaced=0
version_had_previous=0
private_diagnostics_backup="$tmp_dir/BenheimPrivateDiagnostics.previous.cfg"
private_diagnostics_touched=0
private_diagnostics_had_previous=0

cleanup() {
  status=$?
  trap - EXIT

  if [[ "$status" -ne 0 ]]; then
    if [[ -n "$backup_updater_app" && -e "$backup_updater_app" ]]; then
      rm -rf "$updater_app"
      mv "$backup_updater_app" "$updater_app"
    fi
    if [[ "$app_installed" == "1" ]]; then
      rm -rf "$app"
    fi
    if [[ -n "$backup_app" && -e "$backup_app" ]]; then
      rm -rf "$app"
      mv "$backup_app" "$app"
    fi
    if [[ "$plugin_replaced" == "1" ]]; then
      if [[ "$plugin_had_previous" == "1" ]]; then
        install -m 0644 "$plugin_backup" "$plugin"
      else
        rm -f "$plugin"
      fi
    fi
    if [[ "$version_replaced" == "1" ]]; then
      if [[ "$version_had_previous" == "1" ]]; then
        install -m 0644 "$version_backup" "$installed_version"
      else
        rm -f "$installed_version"
      fi
    fi
    if [[ "$private_diagnostics_touched" == "1" ]]; then
      if [[ "$private_diagnostics_had_previous" == "1" ]]; then
        install -m 0600 "$private_diagnostics_backup" "$private_diagnostics"
      else
        rm -f "$private_diagnostics"
      fi
    fi
  fi

  if [[ -n "$staged_app" ]]; then
    rm -rf "$staged_app"
  fi
  rm -rf "$tmp_dir"

  if [[ "${BENHEIM_QOL_NONINTERACTIVE:-0}" != "1" ]]; then
    printf '\nPress Return to close this window.'
    read -r _ || true
  fi

  exit "$status"
}
trap cleanup EXIT

fail() {
  echo "$1" >&2
  exit 1
}

valheim_running() {
  pgrep -x valheim >/dev/null 2>&1 \
    || pgrep -x valheim.x86_64 >/dev/null 2>&1 \
    || pgrep -f "$game_dir/valheim.app/Contents/MacOS" >/dev/null 2>&1
}

if [[ "$(uname -m)" == "arm64" ]] && ! arch -x86_64 /usr/bin/true >/dev/null 2>&1; then
  fail "Rosetta 2 is required on Apple Silicon. Install Rosetta, then run this installer again."
fi

if valheim_running; then
  fail "Valheim is running. Quit the game completely, then run the installer again."
fi

if [[ ! -d "$game_dir/valheim.app" ]]; then
  fail "Valheim was not found at: $game_dir. Install it through Steam, then try again."
fi

if [[ ! -f "$game_dir/valheim.app/Contents/Resources/PlayerIcon.icns" ]]; then
  fail "Valheim's app icon is missing. Verify the game files in Steam, then try again."
fi

if [[ ! -f "$dll" ]]; then
  fail "The Benheim plugin file is missing beside the installer."
fi

if [[ ! -f "$launcher_source" ]]; then
  fail "Missing macos-launcher.sh beside the installer."
fi

if [[ ! -f "$version_source" ]] || ! grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$' "$version_source"; then
  fail "Missing or invalid VERSION beside the installer."
fi

if [[ -e "$plugin_dir" && ! -d "$plugin_dir" ]]; then
  fail "Expected a plugin directory but found another kind of file at: $plugin_dir"
fi

if [[ -e "$app" ]]; then
  plist="$app/Contents/Info.plist"
  existing_identifier=""
  if [[ -f "$plist" ]]; then
    existing_identifier="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$plist" 2>/dev/null || true)"
  fi

  if [[ "$existing_identifier" != "com.beneverman.benheim-qol" ]]; then
    fail "Refusing to replace an unrelated or damaged app at: $app"
  fi
fi

if [[ -e "$updater_app" ]]; then
  updater_plist="$updater_app/Contents/Info.plist"
  updater_identifier=""
  if [[ -f "$updater_plist" ]]; then
    updater_identifier="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$updater_plist" 2>/dev/null || true)"
  fi

  if [[ "$updater_identifier" == "com.beneverman.benheim-updater" ]]; then
    updater_app_owned=1
  fi
fi

echo "Downloading the pinned BepInEx runtime..."
curl -fsSL --retry 3 "$bepinex_url" -o "$tmp_dir/BepInExPack.zip"
actual_sha256="$(shasum -a 256 "$tmp_dir/BepInExPack.zip" | awk '{print $1}')"
if [[ "$actual_sha256" != "$bepinex_sha256" ]]; then
  fail "BepInEx checksum mismatch; refusing to install."
fi

unzip -q "$tmp_dir/BepInExPack.zip" -d "$tmp_dir/bepinex"
bepinex_root="$tmp_dir/bepinex/BepInExPack_Valheim"
if [[ ! -f "$bepinex_root/start_game_bepinex.sh" ]]; then
  fail "The BepInEx package had an unexpected layout."
fi

if valheim_running; then
  fail "Valheim started during setup. Quit the game completely, then run the installer again."
fi

echo "Installing BepInEx and Benheim..."
cp -R "$bepinex_root/." "$game_dir/"
chmod +x "$game_dir/start_game_bepinex.sh"
install -d "$plugin_dir"
if [[ -f "$plugin" ]]; then
  cp "$plugin" "$plugin_backup"
  plugin_had_previous=1
fi
if [[ -f "$installed_version" ]]; then
  cp "$installed_version" "$version_backup"
  version_had_previous=1
fi
if [[ -f "$private_diagnostics" ]]; then
  cp "$private_diagnostics" "$private_diagnostics_backup"
  private_diagnostics_had_previous=1
fi
plugin_tmp="$plugin_dir/.BenheimQoL.dll.$$"
install -m 0644 "$dll" "$plugin_tmp"
mv -f "$plugin_tmp" "$plugin"
plugin_replaced=1
version_tmp="$plugin_dir/.VERSION.$$"
install -m 0644 "$version_source" "$version_tmp"
mv -f "$version_tmp" "$installed_version"
version_replaced=1

install -d "$game_dir/BepInEx/config"
private_diagnostics_touched=1
if [[ -f "$private_diagnostics_source" ]]; then
  diagnostics_tmp="$game_dir/BepInEx/config/.BenheimPrivateDiagnostics.cfg.$$"
  install -m 0600 "$private_diagnostics_source" "$diagnostics_tmp"
  mv -f "$diagnostics_tmp" "$private_diagnostics"
else
  rm -f "$private_diagnostics"
fi

# BenheimQoL owns farming now. Leaving the old plugin active would execute two
# Shift-interact and planting handlers against the same player action.
legacy_mass_farming="$game_dir/BepInEx/plugins/MassFarming/MassFarming.dll"
if [[ -f "$legacy_mass_farming" ]]; then
  disabled_dir="$game_dir/BepInEx/disabled/MassFarming"
  install -d "$disabled_dir"
  disabled_mass_farming="$disabled_dir/MassFarming.dll"
  if [[ -f "$disabled_mass_farming" ]]; then
    if cmp -s "$legacy_mass_farming" "$disabled_mass_farming"; then
      rm "$legacy_mass_farming"
    else
      mv "$legacy_mass_farming" "$disabled_dir/MassFarming.$(date +%Y%m%dT%H%M%S).dll"
    fi
  else
    mv "$legacy_mass_farming" "$disabled_mass_farming"
  fi
fi

legacy_mass_farming_config="$game_dir/BepInEx/config/xeio.MassFarming.cfg"
if [[ -f "$legacy_mass_farming_config" ]]; then
  disabled_dir="$game_dir/BepInEx/disabled/MassFarming"
  install -d "$disabled_dir"
  disabled_config="$disabled_dir/xeio.MassFarming.cfg"
  if [[ -f "$disabled_config" ]]; then
    if cmp -s "$legacy_mass_farming_config" "$disabled_config"; then
      rm "$legacy_mass_farming_config"
    else
      mv "$legacy_mass_farming_config" "$disabled_dir/xeio.MassFarming.$(date +%Y%m%dT%H%M%S).cfg"
    fi
  else
    mv "$legacy_mass_farming_config" "$disabled_config"
  fi
fi

echo "Installing the Benheim launcher..."
install -d "$app_parent"
staged_app="$app_parent/.Benheim.app.stage.$$"
backup_app="$app_parent/.Benheim.app.backup.$$"
install -d "$staged_app/Contents/MacOS" "$staged_app/Contents/Resources"
install -m 0755 "$launcher_source" "$staged_app/Contents/MacOS/BenheimQoL"
install -m 0644 \
  "$game_dir/valheim.app/Contents/Resources/PlayerIcon.icns" \
  "$staged_app/Contents/Resources/PlayerIcon.icns"
cat > "$staged_app/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDisplayName</key>
  <string>Benheim</string>
  <key>CFBundleExecutable</key>
  <string>BenheimQoL</string>
  <key>CFBundleIconFile</key>
  <string>PlayerIcon</string>
  <key>CFBundleIdentifier</key>
  <string>com.beneverman.benheim-qol</string>
  <key>CFBundleName</key>
  <string>Benheim</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0</string>
  <key>CFBundleVersion</key>
  <string>1</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
</dict>
</plist>
PLIST

if [[ -e "$app" ]]; then
  mv "$app" "$backup_app"
fi

if mv "$staged_app" "$app"; then
  staged_app=""
  app_installed=1
else
  fail "Could not replace the launcher; the previous launcher was restored."
fi

touch "$app"

backup_updater_app="$app_parent/.Update Benheim.app.backup.$$"
if [[ "$updater_app_owned" == "1" ]]; then
  echo "Removing the retired Benheim updater..."
  mv "$updater_app" "$backup_updater_app"
fi

if [[ -e "$legacy_app" ]]; then
  legacy_identifier=""
  legacy_plist="$legacy_app/Contents/Info.plist"
  if [[ -f "$legacy_plist" ]]; then
    legacy_identifier="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$legacy_plist" 2>/dev/null || true)"
  fi
  if [[ "$legacy_identifier" == "com.beneverman.benheim-qol" ]]; then
    rm -rf "$legacy_app"
  fi
fi

rm -rf "$backup_app" "$backup_updater_app"
echo
echo "Installed Benheim and:"
echo "  $app"
echo
echo "Open Benheim to play. Rerun the installer to update Benheim."
if [[ -f "$private_diagnostics_source" ]]; then
  echo "This PRIVATE TEST install includes automatic typed diagnostic sharing."
fi
