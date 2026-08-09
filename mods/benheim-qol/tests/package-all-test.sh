#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

# Run the fixed-path coordinator in a minimal fixture repo. The real platform
# packagers prove the coordinator passes one canonical DLL to both archives.
fixture="$test_root/fixture with spaces"
scripts="$fixture/scripts"
dll="$fixture/src/bin/Release/netstandard2.1/BenheimQoL.dll"
verify_log="$test_root/verify.log"
mkdir -p "$scripts" "$(dirname "$dll")"
printf 'public const string PluginVersion = "%s";\n' "$version" > "$fixture/src/Plugin.cs"
printf 'fixture release dll\n' > "$dll"

for script in \
  package-all.sh package-macos.sh package-windows.sh \
  install-macos.command macos-launcher.sh 'Install Benheim.cmd' \
  install-windows.ps1 launch-windows.ps1 windows-doorstop-config.ps1; do
  cp "$root/scripts/$script" "$scripts/$script"
done
chmod +x "$scripts/package-all.sh" "$scripts/package-macos.sh" "$scripts/package-windows.sh"
cat > "$scripts/verify.sh" <<SH
#!/usr/bin/env bash
set -euo pipefail
printf 'verified\n' >> "$verify_log"
SH
chmod +x "$scripts/verify.sh"

output="$("$scripts/package-all.sh")"
mac_package="$fixture/dist/Benheim-macOS-$version.zip"
windows_package="$fixture/dist/Benheim-Windows-$version.zip"
test "$output" = "$(printf '%s\n%s' "$mac_package" "$windows_package")"
test "$(wc -l < "$verify_log" | tr -d ' ')" = 1

expected_version="$test_root/VERSION"
printf '%s\n' "$version" > "$expected_version"
expected_hash="$(shasum -a 256 "$dll" | awk '{print $1}')"
for package in "$mac_package" "$windows_package"; do
  test -f "$package"
  extracted="$test_root/$(basename "$package" .zip)"
  unzip -qq "$package" -d "$test_root"
  cmp -s "$dll" "$extracted/BenheimQoL.dll"
  test "$(shasum -a 256 "$extracted/BenheimQoL.dll" | awk '{print $1}')" = "$expected_hash"
  cmp -s "$expected_version" "$extracted/VERSION"
done

if grep -Eq 'gh release|git push|install-local' "$scripts/package-all.sh"; then
  echo "combined local packaging must not publish or install" >&2
  exit 1
fi

echo "combined package workflow produced matching Mac and Windows artifacts"
