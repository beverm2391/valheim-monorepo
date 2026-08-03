#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

game_dir="$test_root/Valheim"
plugin_dir="$game_dir/BepInEx/plugins/BenheimQoL"
release_dir="$test_root/release"
package_dir="$test_root/package/Benheim-macOS-test"
mkdir -p "$plugin_dir" "$release_dir" "$package_dir"
printf 'old-plugin\n' > "$plugin_dir/BenheimQoL.dll"
printf 'new-plugin\n' > "$package_dir/BenheimQoL.dll"
printf '1.2.0\n' > "$package_dir/VERSION"
printf '#!/bin/sh\n' > "$package_dir/macos-launcher.sh"
printf '#!/bin/sh\n' > "$package_dir/update-macos.sh"
cat > "$package_dir/Install Benheim.command" <<'INSTALLER'
#!/bin/bash
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
plugin_dir="$BENHEIM_QOL_GAME_DIR/BepInEx/plugins/BenheimQoL"
mkdir -p "$plugin_dir"
cp "$script_dir/BenheimQoL.dll" "$plugin_dir/BenheimQoL.dll"
cp "$script_dir/VERSION" "$plugin_dir/VERSION"
printf 'called\n' >> "$BENHEIM_UPDATE_TEST_CALLS"
INSTALLER
chmod +x "$package_dir/Install Benheim.command"

asset="$release_dir/Benheim-macOS.zip"
(
  cd "$test_root/package"
  zip -qr "$asset" Benheim-macOS-test
)
(
  cd "$release_dir"
  shasum -a 256 Benheim-macOS.zip > SHA256SUMS.txt
)

calls="$test_root/installer-calls"
run_update() {
  BENHEIM_QOL_GAME_DIR="$game_dir" \
  BENHEIM_UPDATE_BASE_URL="file://$release_dir" \
  BENHEIM_UPDATE_NO_UI=1 \
  BENHEIM_UPDATE_TEST_CALLS="$calls" \
    "$root/scripts/update-macos.sh"
}

run_update >/dev/null
grep -Fqx 'new-plugin' "$plugin_dir/BenheimQoL.dll"
test "$(wc -l < "$calls" | tr -d ' ')" = "1"

# An already-current install reports success without invoking the installer.
run_update | grep -Fq 'already up to date'
test "$(wc -l < "$calls" | tr -d ' ')" = "1"

# A newer local build must never be replaced by an older stable release.
printf '9.0.0\n' > "$plugin_dir/VERSION"
printf 'newer-local-plugin\n' > "$plugin_dir/BenheimQoL.dll"
run_update | grep -Fq 'newer than stable'
grep -Fqx 'newer-local-plugin' "$plugin_dir/BenheimQoL.dll"
test "$(wc -l < "$calls" | tr -d ' ')" = "1"

# A partial or tampered download cannot replace the current plugin.
printf 'old-plugin\n' > "$plugin_dir/BenheimQoL.dll"
printf '%064d  Benheim-macOS.zip\n' 0 > "$release_dir/SHA256SUMS.txt"
if run_update >/dev/null 2>&1; then
  echo "updater accepted a package with the wrong checksum" >&2
  exit 1
fi
grep -Fqx 'old-plugin' "$plugin_dir/BenheimQoL.dll"
test "$(wc -l < "$calls" | tr -d ' ')" = "1"

# An unavailable release also leaves the current plugin unchanged.
if BENHEIM_QOL_GAME_DIR="$game_dir" \
  BENHEIM_UPDATE_BASE_URL="file://$test_root/no-release" \
  BENHEIM_UPDATE_NO_UI=1 \
    "$root/scripts/update-macos.sh" >/dev/null 2>&1; then
  echo "updater succeeded without a release" >&2
  exit 1
fi
grep -Fqx 'old-plugin' "$plugin_dir/BenheimQoL.dll"

# Missing installed state requires the full installer instead of guessing.
rm "$plugin_dir/BenheimQoL.dll"
if run_update >/dev/null 2>&1; then
  echo "updater repaired an unexpected installation without the full installer" >&2
  exit 1
fi

echo "macOS updater safety and idempotency checks passed"
