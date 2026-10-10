#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT
repo="$test_root/repo with spaces"
fixture="$repo/client-mods/benheim"
scripts="$fixture/scripts"
dll="$fixture/src/bin/Release/netstandard2.1/BenheimQoL.dll"
verify_log="$test_root/verify.log"
speech_tmp="$test_root/speech-temp"
mkdir -p "$scripts" "$(dirname "$dll")"
mkdir -p "$speech_tmp"
printf 'public const string PluginVersion = "%s";\n' "$version" > "$fixture/src/Plugin.cs"
printf 'fixture release dll\n' > "$dll"
for script in package-all.sh package-private-test.sh package-macos.sh package-windows.sh \
  install-macos.command check-valheim-stopped.sh macos-launcher.sh 'Install Benheim.cmd' \
  install-windows.ps1 launch-windows.ps1 windows-doorstop-config.ps1 windows-resident-speech.ps1; do
  cp "$root/scripts/$script" "$scripts/$script"
done
cat > "$scripts/verify.sh" <<SH
#!/usr/bin/env bash
set -euo pipefail
printf 'verified\n' >> "$verify_log"
SH
chmod +x "$scripts/verify.sh"
git -C "$repo" init -q
git -C "$repo" config user.name 'Benheim fixture'
git -C "$repo" config user.email 'benheim-fixture@example.invalid'
git -C "$repo" add client-mods/benheim
git -C "$repo" commit -qm 'fixture Benheim source'
commit="$(git -C "$repo" rev-parse HEAD)"
printf 'unrelated work\n' > "$repo/unrelated.txt"
printf 'uncommitted Benheim edit\n' > "$fixture/local-edit.txt"
if env -u OPENROUTER_API_KEY \
  BENHEIM_AXIOM_DATASET=benheim-diagnostics \
  BENHEIM_AXIOM_INGEST_TOKEN=fixture-sentinel \
  "$scripts/package-all.sh" >/dev/null 2>&1; then
  echo "group packaging accepted uncommitted Benheim source" >&2
  exit 1
fi
rm "$fixture/local-edit.txt"
if env -u BENHEIM_AXIOM_INGEST_TOKEN -u OPENROUTER_API_KEY BENHEIM_QOL_SOURCE_COMMIT="$commit" \
  "$scripts/package-all.sh" >/dev/null 2>&1; then
  echo "group packaging accepted missing Axiom credentials" >&2
  exit 1
fi
if env -u OPENROUTER_API_KEY \
  BENHEIM_AXIOM_DATASET=benheim-diagnostics \
  BENHEIM_AXIOM_INGEST_TOKEN=fixture-ingest-token \
  BENHEIM_QOL_SOURCE_COMMIT="$commit" \
  "$scripts/package-all.sh" >/dev/null 2>&1; then
  echo "group packaging accepted missing OpenRouter credentials" >&2
  exit 1
fi
output="$(TMPDIR="$speech_tmp" \
  OPENROUTER_API_KEY=fixture-speech-key \
  BENHEIM_AXIOM_DATASET=benheim-diagnostics \
  BENHEIM_AXIOM_INGEST_TOKEN=fixture-sentinel \
  BENHEIM_QOL_SOURCE_COMMIT="$commit" "$scripts/package-all.sh")"
! grep -Fq 'fixture-speech-key' <<<"$output"
test -z "$(find "$speech_tmp" -mindepth 1 -print -quit)"
mac="$fixture/dist/Benheim-macOS-$version.zip"
windows="$fixture/dist/Benheim-Windows-$version.zip"
test "$output" = "$(printf '%s\n%s' "$mac" "$windows")"
test "$(wc -l < "$verify_log" | tr -d ' ')" = 1
expected_hash="$(shasum -a 256 "$dll" | awk '{print $1}')"
for package in "$mac" "$windows"; do
  test -f "$package"
  extracted="$test_root/$(basename "$package" .zip)"
  unzip -qq "$package" -d "$test_root"
  cmp -s "$dll" "$extracted/BenheimQoL.dll"
  grep -Fxq "build_id=sha256:$expected_hash" "$extracted/AXIOM-DIAGNOSTICS.cfg"
  grep -Fxq 'BENHEIM_PRIVATE_SPEECH_V1' "$extracted/GEORGE-SPEECH.cfg"
  grep -Fxq 'api_key=fixture-speech-key' "$extracted/GEORGE-SPEECH.cfg"
  grep -Fxq "$commit" "$extracted/SOURCE_COMMIT"
done
! grep -R -Fq 'fixture-sentinel' "$fixture/src" "$scripts"
! grep -R -Fq 'fixture-speech-key' "$fixture/src" "$scripts"

echo "private group packages contain matching DLLs and speech configuration"
