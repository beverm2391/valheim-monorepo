#!/usr/bin/env bash
set -euo pipefail

tests_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$tests_dir/.." && pwd)"
verify="$root/scripts/verify.sh"

fail() {
  printf 'verify runner regression: %s\n' "$1" >&2
  exit 1
}

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
fixture_root="$tmp/client-mods/benheim"
mkdir -p "$fixture_root/scripts" "$fixture_root/tests/native-mechanic-transpilers" "$tmp/bin"
cp "$verify" "$fixture_root/scripts/verify.sh"

cat > "$tmp/bin/dotnet" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
case "$*" in
  *NativeMechanicTranspilerTests.csproj) event='native-transpiler-fixture' ;;
  *QuickStackSummaryTests.csproj) event='quick-stack-summary-fixture' ;;
  *) printf 'unexpected dotnet command in synthetic runner: %s\n' "$*" >&2; exit 89 ;;
esac
printf '%s\n' "$event" >> "$RUNNER_LOG"
EOF
chmod +x "$tmp/bin/dotnet"

cat > "$fixture_root/tests/grappling-test.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf 'grappling-test.sh\n' >> "$RUNNER_LOG"
if [[ "${FAIL_STAGE:-}" == grappling-test.sh ]]; then
  exit 23
fi
EOF
cat > "$fixture_root/tests/native-mechanic-transpilers-test.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
dotnet run --project "$(dirname "${BASH_SOURCE[0]}")/native-mechanic-transpilers/NativeMechanicTranspilerTests.csproj"
EOF
chmod +x "$fixture_root/tests/grappling-test.sh" "$fixture_root/tests/native-mechanic-transpilers-test.sh"

make_source_fixture() {
  local name="$1"
  cat > "$fixture_root/tests/$name" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
name="${BASH_SOURCE[0]##*/}"
printf '%s\n' "$name" >> "$RUNNER_LOG"
if [[ "${FAIL_STAGE:-}" == "$name" ]]; then
  exit 23
fi
EOF
  chmod +x "$fixture_root/tests/$name"
}
make_source_fixture alpha-test.sh
make_source_fixture zeta-test.sh

cat > "$fixture_root/scripts/build.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf 'release-build\n' >> "$RUNNER_LOG"
EOF
chmod +x "$fixture_root/scripts/build.sh"

: > "$tmp/pass.log"
RUNNER_LOG="$tmp/pass.log" PATH="$tmp/bin:$PATH" "$fixture_root/scripts/verify.sh" \
  > "$tmp/pass.out" 2> "$tmp/pass.err"
cat > "$tmp/expected-pass.log" <<'EOF'
grappling-test.sh
native-transpiler-fixture
alpha-test.sh
zeta-test.sh
quick-stack-summary-fixture
release-build
EOF
cmp -s "$tmp/expected-pass.log" "$tmp/pass.log" \
  || fail 'synthetic run did not execute Grappling and native fixtures once, first, then complete all stages'
rg -Fq '[verify] Grappling fixtures and native source checks' "$tmp/pass.out" \
  || fail 'Grappling stage was not reported'
rg -Fq '[verify] Native mechanic transpiler fixture' "$tmp/pass.out" \
  || fail 'native-transpiler stage was not reported'
rg -Fq '[verify] Verification passed' "$tmp/pass.out" \
  || fail 'successful completion was not reported'

: > "$tmp/fail.log"
if RUNNER_LOG="$tmp/fail.log" FAIL_STAGE=alpha-test.sh PATH="$tmp/bin:$PATH" \
  "$fixture_root/scripts/verify.sh" > "$tmp/fail.out" 2> "$tmp/fail.err"; then
  fail 'synthetic source-check failure unexpectedly passed'
else
  status=$?
fi
[[ "$status" -eq 23 ]] || fail "failure status changed from 23 to $status"
cat > "$tmp/expected-fail.log" <<'EOF'
grappling-test.sh
native-transpiler-fixture
alpha-test.sh
EOF
cmp -s "$tmp/expected-fail.log" "$tmp/fail.log" \
  || fail 'runner continued after the synthetic source-check failure'
rg -Fq '[verify] FAILED during check: alpha-test.sh (exit 23)' "$tmp/fail.err" \
  || fail 'failure output did not identify the failed stage and status'

# A quick-fixture failure must stop before every later source check or build.
: > "$tmp/early-fail.log"
if RUNNER_LOG="$tmp/early-fail.log" FAIL_STAGE=grappling-test.sh PATH="$tmp/bin:$PATH" \
  "$fixture_root/scripts/verify.sh" > "$tmp/early-fail.out" 2> "$tmp/early-fail.err"; then
  fail 'synthetic early-fixture failure unexpectedly passed'
else
  status=$?
fi
[[ "$status" -eq 23 ]] || fail "early fixture status changed from 23 to $status"
printf 'grappling-test.sh\n' > "$tmp/expected-early-fail.log"
cmp -s "$tmp/expected-early-fail.log" "$tmp/early-fail.log" \
  || fail 'runner continued after the early fixture failed'
rg -Fq '[verify] FAILED during Grappling fixtures and native source checks (exit 23)' "$tmp/early-fail.err" \
  || fail 'early failure output did not identify the fixture stage'

printf 'verify runner order, once-only execution, and fail-fast checks passed\n'
