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
expected_dll="$test_root/expected.dll"
printf 'fixture release dll\n' > "$expected_dll"
for script in package-all.sh package-private-test.sh package-macos.sh package-windows.sh \
  install-macos.command check-valheim-stopped.sh macos-launcher.sh 'Install Benheim.cmd' \
  install-windows.ps1 launch-windows.ps1 windows-doorstop-config.ps1 windows-resident-speech.ps1; do
  cp "$root/scripts/$script" "$scripts/$script"
done
cat > "$scripts/verify.sh" <<SH
#!/usr/bin/env bash
set -euo pipefail
printf 'verified\n' >> "$verify_log"
candidate_root="\$(cd "\$(dirname "\${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "\$candidate_root/src/bin/Release/netstandard2.1"
printf 'fixture release dll\n' > "\$candidate_root/src/bin/Release/netstandard2.1/BenheimQoL.dll"
if [[ -n "\${FIXTURE_MUTATION_ROOT:-}" ]]; then
  printf 'public const string PluginVersion = "9.9.9";\n' > "\$FIXTURE_MUTATION_ROOT/src/Plugin.cs"
  printf 'later unverified DLL\n' > "\$FIXTURE_MUTATION_ROOT/src/bin/Release/netstandard2.1/BenheimQoL.dll"
  printf '\nlater unverified installer\n' >> "\$FIXTURE_MUTATION_ROOT/scripts/install-macos.command"
fi
if [[ "\${FIXTURE_VERIFY_FAIL:-0}" == 1 ]]; then
  exit 23
fi
SH
chmod +x "$scripts/verify.sh"
# Inject a bad final payload after the real platform packager has validated
# and copied its inputs. The coordinator must inspect the actual ZIP bytes.
cat >> "$scripts/package-windows.sh" <<'SH'
if [[ "${FIXTURE_CORRUPT_ARCHIVE:-0}" == 1 ]]; then
  printf 'corrupt archive DLL\n' > "$stage/BenheimQoL.dll"
  (cd "$dist" && zip -q "$package_name.zip" "$package_name/BenheimQoL.dll")
fi
SH
git -C "$repo" init -q
git -C "$repo" config user.name 'Benheim fixture'
git -C "$repo" config user.email 'benheim-fixture@example.invalid'
printf '%s\n' 'client-mods/benheim/dist/' 'client-mods/benheim/src/bin/' 'client-mods/benheim/src/obj/' > "$repo/.gitignore"
git -C "$repo" add .gitignore client-mods/benheim
git -C "$repo" commit -qm 'fixture Benheim source'
commit="$(git -C "$repo" rev-parse HEAD)"
test -z "$(git -C "$repo" ls-tree -r --name-only "$commit" -- client-mods/benheim/src/bin)"
package_fixture() {
  env TMPDIR="$speech_tmp" \
    OPENROUTER_API_KEY=fixture-speech-key \
    BENHEIM_AXIOM_DATASET=benheim-diagnostics \
    BENHEIM_AXIOM_INGEST_TOKEN=fixture-sentinel \
    BENHEIM_QOL_SOURCE_COMMIT="$commit" "$@" "$scripts/package-all.sh"
}
printf 'unrelated work\n' > "$repo/unrelated.txt"
printf 'uncommitted Benheim edit\n' > "$fixture/local-edit.txt"
if package_fixture > "$test_root/dirty-source.log" 2>&1; then
  echo "group packaging accepted uncommitted Benheim source" >&2
  exit 1
fi
grep -Fq 'Group packages require committed Benheim source.' "$test_root/dirty-source.log"
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

test ! -e "$verify_log"
output="$(package_fixture)"
! grep -Fq 'fixture-speech-key' <<<"$output"
test -z "$(find "$speech_tmp" -mindepth 1 -print -quit)"
mac="$fixture/dist/Benheim-macOS-$version.zip"
windows="$fixture/dist/Benheim-Windows-$version.zip"
test "$output" = "$(printf '%s\n%s' "$mac" "$windows")"
test "$(wc -l < "$verify_log" | tr -d ' ')" = 1
expected_hash="$(shasum -a 256 "$expected_dll" | awk '{print $1}')"
assert_packages() {
  for package in "$mac" "$windows"; do
    test -f "$package"
    extracted="$test_root/$(basename "$package" .zip)"
    rm -rf "$extracted"
    unzip -qq "$package" -d "$test_root"
    cmp -s "$expected_dll" "$extracted/BenheimQoL.dll"
    grep -Fxq "build_id=sha256:$expected_hash" "$extracted/AXIOM-DIAGNOSTICS.cfg"
    grep -Fxq 'BENHEIM_PRIVATE_SPEECH_V1' "$extracted/GEORGE-SPEECH.cfg"
    grep -Fxq 'api_key=fixture-speech-key' "$extracted/GEORGE-SPEECH.cfg"
    grep -Fxq "$commit" "$extracted/SOURCE_COMMIT"
    grep -Fxq "$version" "$extracted/VERSION"
  done
}
assert_packages
! grep -R -Fq 'fixture-sentinel' "$fixture/src" "$scripts"
! grep -R -Fq 'fixture-speech-key' "$fixture/src" "$scripts"

# A later edit to the caller's checkout must not affect the selected commit's
# version, verified DLL, or installer. Verification still runs only once.
package_fixture FIXTURE_MUTATION_ROOT="$fixture" >/dev/null
test "$(wc -l < "$verify_log" | tr -d ' ')" = 2
assert_packages
git -C "$repo" show "$commit:client-mods/benheim/scripts/install-macos.command" > "$test_root/expected-installer"
cmp -s "$test_root/expected-installer" "$test_root/Benheim-macOS-$version/Install Benheim.command"
test ! -e "$fixture/dist/Benheim-macOS-9.9.9.zip"
git -C "$repo" restore --worktree client-mods/benheim

# Failed verification and a corrupted final archive must both leave a prior
# successful pair intact, with no partial new candidate or temporary secrets.
prior_mac_hash="$(shasum -a 256 "$mac" | awk '{print $1}')"
prior_windows_hash="$(shasum -a 256 "$windows" | awk '{print $1}')"
for failure in FIXTURE_VERIFY_FAIL=1 FIXTURE_CORRUPT_ARCHIVE=1; do
  if package_fixture "$failure" > "$test_root/failure.log" 2>&1; then
    printf 'group packaging accepted injected failure: %s\n' "$failure" >&2
    exit 1
  fi
  test "$(shasum -a 256 "$mac" | awk '{print $1}')" = "$prior_mac_hash"
  test "$(shasum -a 256 "$windows" | awk '{print $1}')" = "$prior_windows_hash"
  test -z "$(find "$speech_tmp" -mindepth 1 -print -quit)"
done
grep -Fq 'stage=archive_check platform=Windows status=failed' "$test_root/failure.log"
test "$(wc -l < "$verify_log" | tr -d ' ')" = 4

# mv accepts a directory as its destination. That must never produce a ready
# marker pointing at a directory instead of the promised archive file.
mv "$mac" "$test_root/prior-mac.zip"
mkdir "$mac"
if package_fixture > "$test_root/publish-failure.log" 2>&1; then
  echo 'group packaging accepted a directory as its final archive path' >&2
  exit 1
fi
grep -Fq 'stage=publish platform=macOS status=failed' "$test_root/publish-failure.log"
! grep -Fq 'stage=complete' "$test_root/publish-failure.log"
test -z "$(find "$speech_tmp" -mindepth 1 -print -quit)"

echo "private group packages verify one committed snapshot and reject mismatched archive bytes"
