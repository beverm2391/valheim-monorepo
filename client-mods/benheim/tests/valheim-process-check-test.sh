#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT
mock_bin="$test_root/bin"
calls="$test_root/pgrep-calls"
mkdir -p "$mock_bin"

cat > "$mock_bin/pgrep" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$MOCK_PGREP_CALLS"
if [[ "${MOCK_RUNNING_EXECUTABLE:-}" == "${2:-}" ]]; then
  echo 4242
  exit 0
fi
exit 1
EOF
chmod +x "$mock_bin/pgrep"

stopped_output="$(
  PATH="$mock_bin:$PATH" MOCK_PGREP_CALLS="$calls" \
    "$root/scripts/check-valheim-stopped.sh"
)"
test "$stopped_output" = "Valheim is not running."
expected_calls="$(printf '%s\n' '-x valheim' '-x valheim.x86_64')"
test "$(cat "$calls")" = "$expected_calls"
! grep -Eq -- '(^| )-f( |$)' "$calls"

: > "$calls"
if PATH="$mock_bin:$PATH" MOCK_PGREP_CALLS="$calls" \
  MOCK_RUNNING_EXECUTABLE=valheim \
  "$root/scripts/check-valheim-stopped.sh" >"$test_root/running.out" 2>"$test_root/running.err"; then
  echo "process check accepted a running Valheim executable" >&2
  exit 1
fi
grep -Fq 'Valheim is running (valheim:4242)' "$test_root/running.err"

: > "$calls"
if PATH="$mock_bin:$PATH" MOCK_PGREP_CALLS="$calls" \
  MOCK_RUNNING_EXECUTABLE=valheim.x86_64 \
  "$root/scripts/check-valheim-stopped.sh" --quiet \
  >"$test_root/quiet.out" 2>"$test_root/quiet.err"; then
  echo "quiet process check accepted a running Valheim executable" >&2
  exit 1
fi
test ! -s "$test_root/quiet.out"
test ! -s "$test_root/quiet.err"

grep -Fq 'process_check=' "$root/scripts/install-macos.command"
grep -Fq '"$process_check" --quiet' "$root/scripts/install-macos.command"
! grep -Eq 'pgrep .*valheim' "$root/scripts/install-macos.command"

echo "exact Valheim process identity checks passed"
