#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
helper="$root/scripts/decompile-valheim.sh"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

assembly_dir="$test_root/Game With Spaces/Managed Files"
assembly="$assembly_dir/assembly valheim.dll"
tool_dir="$test_root/Tool With Spaces"
fake_ilspy="$tool_dir/fake ilspycmd"
cache_dir="$test_root/Cache With Spaces"
invoke_log="$test_root/invocations.log"
mkdir -p "$assembly_dir" "$tool_dir"
printf 'assembly-v1\n' > "$assembly"
resolved_assembly="$(cd "$assembly_dir" && pwd -P)/$(basename "$assembly")"

cat > "$fake_ilspy" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf 'called\n' >> "$FAKE_ILSPY_LOG"
case "${FAKE_ILSPY_MODE:-success}" in
  fail)
    printf 'partial source must not escape\n'
    printf 'synthetic decompiler failure\n' >&2
    exit 7
    ;;
  empty)
    exit 0
    ;;
esac

requested_type=""
while [[ "$#" -gt 0 ]]; do
  if [[ "$1" == "-t" ]]; then
    requested_type="$2"
    shift 2
    continue
  fi
  shift
done
printf '// fake source for %s\n' "$requested_type"
EOF
chmod +x "$fake_ilspy"

run_helper() {
  local mode="$1"
  local type="$2"
  local stdout_file="$3"
  local stderr_file="$4"
  FAKE_ILSPY_LOG="$invoke_log" \
  FAKE_ILSPY_MODE="$mode" \
  VALHEIM_ASSEMBLY_PATH="$assembly" \
  VALHEIM_ILSPY_PATH="$fake_ilspy" \
  VALHEIM_DECOMPILE_CACHE_DIR="$cache_dir" \
    "$helper" "$type" > "$stdout_file" 2> "$stderr_file"
}

first_out="$test_root/first.out"
first_err="$test_root/first.err"
run_helper success Character "$first_out" "$first_err"
grep -Fxq '// fake source for Character' "$first_out"
! grep -Fq 'decompile-valheim:' "$first_out"
grep -Fq "decompile-valheim: assembly=$resolved_assembly" "$first_err"
grep -Eq '^decompile-valheim: sha256=[0-9a-f]{64}$' "$first_err"
grep -Fxq 'decompile-valheim: type=Character' "$first_err"
grep -Fxq 'decompile-valheim: cache=miss' "$first_err"
[[ "$(wc -l < "$invoke_log" | tr -d ' ')" == "1" ]]

second_out="$test_root/second.out"
second_err="$test_root/second.err"
run_helper fail Character "$second_out" "$second_err"
cmp -s "$first_out" "$second_out"
grep -Fxq 'decompile-valheim: cache=hit' "$second_err"
[[ "$(wc -l < "$invoke_log" | tr -d ' ')" == "1" ]]

printf 'assembly-v2\n' >> "$assembly"
third_out="$test_root/third.out"
third_err="$test_root/third.err"
run_helper success Character "$third_out" "$third_err"
grep -Fxq 'decompile-valheim: cache=miss' "$third_err"
[[ "$(wc -l < "$invoke_log" | tr -d ' ')" == "2" ]]

failure_out="$test_root/failure.out"
failure_err="$test_root/failure.err"
if run_helper fail FailureType "$failure_out" "$failure_err"; then
  printf 'failed decompilation unexpectedly succeeded\n' >&2
  exit 1
fi
[[ ! -s "$failure_out" ]]
grep -Fq 'ILSpy failed to decompile type' "$failure_err"
run_helper success FailureType "$test_root/retry.out" "$test_root/retry.err"
grep -Fxq 'decompile-valheim: cache=miss' "$test_root/retry.err"

if run_helper empty EmptyType "$test_root/empty.out" "$test_root/empty.err"; then
  printf 'empty decompilation unexpectedly succeeded\n' >&2
  exit 1
fi
grep -Fq 'ILSpy returned empty source' "$test_root/empty.err"
run_helper success EmptyType "$test_root/empty-retry.out" "$test_root/empty-retry.err"
grep -Fxq 'decompile-valheim: cache=miss' "$test_root/empty-retry.err"

if "$helper" > /dev/null 2> "$test_root/no-type.err"; then
  printf 'missing type unexpectedly succeeded\n' >&2
  exit 1
fi
grep -Fq 'usage:' "$test_root/no-type.err"

if "$helper" '' > /dev/null 2> "$test_root/blank-type.err"; then
  printf 'blank type unexpectedly succeeded\n' >&2
  exit 1
fi
grep -Fq 'invalid type' "$test_root/blank-type.err"

if "$helper" 'Bad/Type' > /dev/null 2> "$test_root/invalid-type.err"; then
  printf 'invalid type unexpectedly succeeded\n' >&2
  exit 1
fi
grep -Fq 'invalid type' "$test_root/invalid-type.err"

if "$helper" Character Player > /dev/null 2> "$test_root/extra-type.err"; then
  printf 'extra type unexpectedly succeeded\n' >&2
  exit 1
fi
grep -Fq 'usage:' "$test_root/extra-type.err"

if VALHEIM_ASSEMBLY_PATH="$test_root/missing.dll" \
  "$helper" Character > /dev/null 2> "$test_root/missing-assembly.err"; then
  printf 'missing assembly unexpectedly succeeded\n' >&2
  exit 1
fi
grep -Fq 'Valheim assembly not found' "$test_root/missing-assembly.err"

printf 'uncached assembly\n' > "$assembly"
if VALHEIM_ASSEMBLY_PATH="$assembly" \
  VALHEIM_ILSPY_PATH="$test_root/missing ilspycmd" \
  VALHEIM_DECOMPILE_CACHE_DIR="$test_root/missing-tool-cache" \
  "$helper" MissingToolType > /dev/null 2> "$test_root/missing-tool.err"; then
  printf 'missing ILSpy unexpectedly succeeded\n' >&2
  exit 1
fi
grep -Fq 'ILSpy override is not executable' "$test_root/missing-tool.err"

printf 'cached Valheim type decompilation checks passed\n'
