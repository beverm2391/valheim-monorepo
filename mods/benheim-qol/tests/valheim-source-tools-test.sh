#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
ensure="$root/scripts/ensure-valheim-source.sh"
search="$root/scripts/search-valheim-source.sh"
list="$root/scripts/list-valheim-types.sh"
diff_types="$root/scripts/diff-valheim-types.sh"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT
assembly_dir="$test_root/Game With Spaces/Managed Files"; assembly="$assembly_dir/assembly valheim.dll"
tool_dir="$test_root/Tool With Spaces"; fake_ilspy="$tool_dir/fake ilspycmd"
cache_dir="$test_root/Cache With Spaces"; invoke_log="$test_root/ilspy-invocations.log"
mkdir -p "$assembly_dir" "$tool_dir"
cat > "$fake_ilspy" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
mode="${FAKE_ILSPY_MODE:-success}"
log="${FAKE_ILSPY_LOG:?FAKE_ILSPY_LOG is required}"
kind="other"
output_dir=''
assembly_path=''
requested_type=''
while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --version)
      printf '%s %s\n' "$mode" version >> "$log"
      printf 'fake-ilspy 1.0\n'
      exit 0
      ;;
    -p)
      kind='project'
      shift
      ;;
    -l)
      kind='types'
      shift 2
      ;;
    -o)
      output_dir="$2"
      shift 2
      ;;
    -t)
      kind='type'
      requested_type="$2"
      shift 2
      ;;
    *)
      assembly_path="$1"
      shift
      ;;
  esac
done
printf '%s %s\n' "$mode" "$kind" >> "$log"
if [[ "$kind" == project ]]; then
  if [[ "$mode" == slow ]]; then
    sleep "${FAKE_ILSPY_SLEEP_SECONDS:-1}"
  fi
  if [[ "$mode" == fail ]]; then
    mkdir -p "$output_dir/partial"
    printf 'partial output\n' > "$output_dir/partial/Partial.cs"
    printf 'synthetic project failure\n' >&2
    exit 7
  fi
  mkdir -p "$output_dir/Game"
  case "$mode" in
    incomplete-csproj)
      printf '<Project />\n' > "$output_dir/Game/Game.csproj"
      ;;
    incomplete-cs)
      printf 'namespace Game { public class Partial {} }\n' > "$output_dir/Game/Partial.cs"
      ;;
    *)
      printf '<Project Sdk="Microsoft.NET.Sdk" />\n' > "$output_dir/Game/Game.csproj"
      player_marker='v1'
      if grep -Fq 'assembly-v2' "$assembly_path"; then
        player_marker='v2'
      fi
      printf '%s\n' 'namespace Game { public class Character { public class Nested { } public string Marker => "stable"; } }' > "$output_dir/Game/Character.cs"
      printf 'namespace Game { public class Player { public string Marker => "%s"; } }\n' "$player_marker" > "$output_dir/Game/Player.cs"
      printf '%s\n' 'namespace Game { public interface IInteractable { } }' > "$output_dir/Game/IInteractable.cs"
      printf '%s\n' 'namespace Game { public struct Vector { } }' > "$output_dir/Game/Vector.cs"
      printf '%s\n' 'namespace Game { public delegate DamageHandler DamageHandler(); }' > "$output_dir/Game/DamageHandler.cs"
      printf '%s\n' 'namespace Game { public enum DamageType { Physical, Fire } }' > "$output_dir/Game/DamageType.cs"
      printf '%s\n' 'public class Nested { }' > "$output_dir/Nested.cs"
      ;;
  esac
  exit 0
fi
if [[ "$kind" == types ]]; then
  case "$mode" in
    empty)
      exit 0
      ;;
    malformed)
      printf 'not type metadata\n'
      exit 0
      ;;
    *)
      cat <<'TYPES'
Class Game.Character
Class Game.Character.Nested
Class Game.Player
Class Nested
Interface Game.IInteractable
Struct Game.Vector
Delegate Game.DamageHandler
Enum Game.DamageType
TYPES
      exit 0
      ;;
  esac
fi
if [[ "$kind" == type ]]; then
  printf '// fake source for %s\n' "$requested_type"
  exit 0
fi
printf 'unexpected fake ILSpy invocation\n' >&2
exit 11
EOF
chmod +x "$fake_ilspy"
run_ensure() {
  local mode="$1" stdout_file="$2" stderr_file="$3"
  FAKE_ILSPY_LOG="$invoke_log" \
  FAKE_ILSPY_MODE="$mode" \
  FAKE_ILSPY_SLEEP_SECONDS="${FAKE_ILSPY_SLEEP_SECONDS:-1}" \
  VALHEIM_ASSEMBLY_PATH="$assembly" \
  VALHEIM_ILSPY_PATH="$fake_ilspy" \
  VALHEIM_SOURCE_CACHE_DIR="$cache_dir" \
  VALHEIM_SOURCE_LOCK_TIMEOUT_SECONDS=10 \
    "$ensure" > "$stdout_file" 2> "$stderr_file"
}
run_search() {
  local stdout_file="$1" stderr_file="$2"
  shift 2
  FAKE_ILSPY_LOG="$invoke_log" \
  FAKE_ILSPY_MODE=success \
  VALHEIM_ASSEMBLY_PATH="$assembly" \
  VALHEIM_ILSPY_PATH="$fake_ilspy" \
  VALHEIM_SOURCE_CACHE_DIR="$cache_dir" \
    "$search" "$@" > "$stdout_file" 2> "$stderr_file"
}
run_list() {
  local stdout_file="$1" stderr_file="$2"
  shift 2
  FAKE_ILSPY_LOG="$invoke_log" \
  FAKE_ILSPY_MODE=success \
  VALHEIM_ASSEMBLY_PATH="$assembly" \
  VALHEIM_ILSPY_PATH="$fake_ilspy" \
  VALHEIM_SOURCE_CACHE_DIR="$cache_dir" \
    "$list" "$@" > "$stdout_file" 2> "$stderr_file"
}
run_diff() {
  local stdout_file="$1" stderr_file="$2"
  shift 2
  FAKE_ILSPY_LOG="$invoke_log" \
  VALHEIM_ILSPY_PATH="$fake_ilspy" \
  VALHEIM_SOURCE_CACHE_DIR="$cache_dir" \
    "$diff_types" "$@" > "$stdout_file" 2> "$stderr_file"
}
assert_no_published_tree() {
  local sha="$1" id="$2"
  local version_root="$cache_dir/$sha"
  [[ ! -e "$version_root/projects/$id" ]]
  [[ -z "$(find "$version_root/projects" -mindepth 1 -maxdepth 1 ! -name '.locks' -print -quit 2>/dev/null)" ]]
}
printf 'assembly-v1\n' > "$assembly"
: > "$invoke_log"
first_out="$test_root/first.out"
first_err="$test_root/first.err"
run_ensure success "$first_out" "$first_err"
first_tree="$(cat "$first_out")"
[[ -d "$first_tree" ]]
grep -Fqx 'ensure-valheim-source: cache=miss' "$first_err"
grep -Fq "ensure-valheim-source: assembly=$(cd "$assembly_dir" && pwd -P)/$(basename "$assembly")" "$first_err"
grep -Eq '^ensure-valheim-source: sha256=[0-9a-f]{64}$' "$first_err"
grep -Eq '^ensure-valheim-source: decompiler=ilspy-[0-9a-f]{64}$' "$first_err"
first_sha="$(awk -F= '/ensure-valheim-source: sha256=/{print $2; exit}' "$first_err")"
first_id="$(awk -F= '/ensure-valheim-source: decompiler=/{print $2; exit}' "$first_err")"
[[ -f "$first_tree/.benheim/COMPLETE" ]]
[[ -s "$first_tree/.benheim/types.txt" ]]
[[ "$(awk '$2 == "project" { count++ } END { print count + 0 }' "$invoke_log")" == 1 ]]
[[ "$(awk '$2 == "types" { count++ } END { print count + 0 }' "$invoke_log")" == 1 ]]
warm_out="$test_root/warm.out"
warm_err="$test_root/warm.err"
run_ensure fail "$warm_out" "$warm_err"
cmp -s "$first_out" "$warm_out"
grep -Fqx 'ensure-valheim-source: cache=hit' "$warm_err"
[[ "$(awk '$2 == "project" { count++ } END { print count + 0 }' "$invoke_log")" == 1 ]]
[[ "$(awk '$2 == "types" { count++ } END { print count + 0 }' "$invoke_log")" == 1 ]]
printf 'assembly-v2\n' > "$assembly"
second_out="$test_root/second.out"
second_err="$test_root/second.err"
run_ensure success "$second_out" "$second_err"
second_tree="$(cat "$second_out")"
second_sha="$(awk -F= '/ensure-valheim-source: sha256=/{print $2; exit}' "$second_err")"
[[ "$second_sha" != "$first_sha" ]]
[[ "$second_tree" != "$first_tree" ]]
[[ -d "$first_tree" ]]
[[ -f "$first_tree/.benheim/COMPLETE" ]]
grep -Fqx 'ensure-valheim-source: cache=miss' "$second_err"
[[ "$(awk '$2 == "project" { count++ } END { print count + 0 }' "$invoke_log")" == 2 ]]
[[ "$(awk '$2 == "types" { count++ } END { print count + 0 }' "$invoke_log")" == 2 ]]
all_types_out="$test_root/all-types.out"
all_types_err="$test_root/all-types.err"
run_list "$all_types_out" "$all_types_err"
grep -Fxq 'Class Game.Character' "$all_types_out"
grep -Fxq 'Class Game.Character.Nested' "$all_types_out"
grep -Fxq 'Class Game.Player' "$all_types_out"
grep -Fxq 'Class Nested' "$all_types_out"
grep -Fxq 'Interface Game.IInteractable' "$all_types_out"
grep -Fxq 'Struct Game.Vector' "$all_types_out"
grep -Fxq 'Delegate Game.DamageHandler' "$all_types_out"
grep -Fxq 'Enum Game.DamageType' "$all_types_out"
[[ "$(wc -l < "$all_types_out" | tr -d ' ')" == 8 ]]
grep -Fq 'list-valheim-types: tree=' "$all_types_err"
damage_out="$test_root/damage-types.out"
damage_err="$test_root/damage-types.err"
run_list "$damage_out" "$damage_err" --kind enum damage
grep -Fxq 'Enum Game.DamageType' "$damage_out"
[[ "$(wc -l < "$damage_out" | tr -d ' ')" == 1 ]]
character_out="$test_root/character-types.out"
character_err="$test_root/character-types.err"
run_list "$character_out" "$character_err" --kind class CHAR
grep -Fxq 'Class Game.Character' "$character_out"
[[ "$(wc -l < "$character_out" | tr -d ' ')" == 1 ]]
interface_out="$test_root/interface-types.out"
interface_err="$test_root/interface-types.err"
run_list "$interface_out" "$interface_err" --kind interface interact
grep -Fxq 'Interface Game.IInteractable' "$interface_out"
[[ "$(wc -l < "$interface_out" | tr -d ' ')" == 1 ]]
search_match_out="$test_root/search-match.out"
search_match_err="$test_root/search-match.err"
run_search "$search_match_out" "$search_match_err" -n --glob '*.cs' 'Marker => "v2"'
grep -Fq 'Game/Player.cs' "$search_match_out"
grep -Fq 'search-valheim-source: tree=' "$search_match_err"
grep -Fq 'ensure-valheim-source: cache=hit' "$search_match_err"
! grep -Fq 'ensure-valheim-source:' "$search_match_out"
search_none_out="$test_root/search-none.out"
search_none_err="$test_root/search-none.err"
if run_search "$search_none_out" "$search_none_err" --glob '*.cs' 'definitely-not-present'; then
  printf 'search no-match unexpectedly returned success\n' >&2
  exit 1
else
  search_none_status=$?
fi
[[ "$search_none_status" == 1 ]]
[[ ! -s "$search_none_out" ]]
grep -Fq 'search-valheim-source: tree=' "$search_none_err"
grep -Fq 'ensure-valheim-source: cache=hit' "$search_none_err"
search_error_out="$test_root/search-error.out"
search_error_err="$test_root/search-error.err"
if run_search "$search_error_out" "$search_error_err" --glob '*.cs' '['; then
  printf 'search invalid pattern unexpectedly returned success\n' >&2
  exit 1
else
  search_error_status=$?
fi
[[ "$search_error_status" == 2 ]]
[[ ! -s "$search_error_out" ]]
grep -Fq 'search-valheim-source: tree=' "$search_error_err"
grep -Fq 'ensure-valheim-source: cache=hit' "$search_error_err"
prefix_first="${first_sha:0:12}"
prefix_second="${second_sha:0:12}"
equal_out="$test_root/diff-equal.out"
equal_err="$test_root/diff-equal.err"
equal_status=0
run_diff "$equal_out" "$equal_err" "$prefix_first" "$prefix_second" --type Game.Character || equal_status=$?
if [[ "$equal_status" != 0 ]]; then
  cat "$equal_err" >&2
  exit "$equal_status"
fi
[[ ! -s "$equal_out" ]]
grep -Fq 'diff-valheim-types: left=' "$equal_err"
grep -Fq 'diff-valheim-types: right=' "$equal_err"
delegate_out="$test_root/diff-delegate.out"
delegate_err="$test_root/diff-delegate.err"
run_diff "$delegate_out" "$delegate_err" "$first_sha" "$second_sha" --type Game.DamageHandler
[[ ! -s "$delegate_out" ]]
grep -Fq 'diff-valheim-types: type=Game.DamageHandler kind=Delegate' "$delegate_err"
nested_out="$test_root/diff-nested.out"
nested_err="$test_root/diff-nested.err"
run_diff "$nested_out" "$nested_err" "$first_sha" "$second_sha" --type Game.Character.Nested
[[ ! -s "$nested_out" ]]
grep -Fq 'diff-valheim-types: type=Game.Character.Nested kind=Class' "$nested_err"
different_out="$test_root/diff-different.out"
different_err="$test_root/diff-different.err"
if run_diff "$different_out" "$different_err" "$first_sha" "$prefix_second" --type Game.Player; then
  printf 'different selected type unexpectedly compared equal\n' >&2
  exit 1
else
  different_status=$?
fi
[[ "$different_status" == 1 ]]
grep -Fq 'Marker => "v1"' "$different_out"
grep -Fq 'Marker => "v2"' "$different_out"
grep -Fq 'diff-valheim-types: type=Game.Player kind=Class' "$different_err"
no_type_out="$test_root/diff-no-type.out"
no_type_err="$test_root/diff-no-type.err"
if run_diff "$no_type_out" "$no_type_err" "$first_sha" "$second_sha"; then
  printf 'whole-tree diff default unexpectedly succeeded\n' >&2
  exit 1
else
  no_type_status=$?
fi
[[ "$no_type_status" == 2 ]]
[[ ! -s "$no_type_out" ]]
grep -Fq 'usage:' "$no_type_err"
ambiguous_sha="${first_sha:0:8}00000000000000000000000000000000000000000000000000000000"
if [[ "$ambiguous_sha" == "$first_sha" || "$ambiguous_sha" == "$second_sha" ]]; then
  printf 'synthetic ambiguous selector unexpectedly matched a real SHA\n' >&2
  exit 1
fi
ambiguous_tree="$cache_dir/$ambiguous_sha/projects/$first_id"
mkdir -p "$(dirname "$ambiguous_tree")"
cp -R "$first_tree" "$ambiguous_tree"
awk -v sha="$ambiguous_sha" '{ if ($0 ~ /^assembly_sha256=/) print "assembly_sha256=" sha; else print }' \
  "$ambiguous_tree/.benheim/manifest" > "$ambiguous_tree/.benheim/manifest.new"
mv "$ambiguous_tree/.benheim/manifest.new" "$ambiguous_tree/.benheim/manifest"
ambiguous_out="$test_root/diff-ambiguous.out"
ambiguous_err="$test_root/diff-ambiguous.err"
ambiguous_prefix="${first_sha:0:8}"
if run_diff "$ambiguous_out" "$ambiguous_err" "$ambiguous_prefix" "$second_sha" --type Game.Character; then
  printf 'ambiguous assembly prefix unexpectedly succeeded\n' >&2
  exit 1
else
  ambiguous_status=$?
fi
[[ "$ambiguous_status" == 2 ]]
grep -Fq 'is ambiguous' "$ambiguous_err"
missing_sha='ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff'
if [[ -e "$cache_dir/$missing_sha" ]]; then
  printf 'synthetic missing selector unexpectedly exists\n' >&2
  exit 1
fi
missing_out="$test_root/diff-missing.out"
missing_err="$test_root/diff-missing.err"
if run_diff "$missing_out" "$missing_err" "$missing_sha" "$second_sha" --type Game.Character; then
  printf 'missing assembly selector unexpectedly succeeded\n' >&2
  exit 1
else
  missing_status=$?
fi
[[ "$missing_status" == 2 ]]
grep -Fq 'did not match a cached version' "$missing_err"
incomplete_sha='eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee'
if [[ -e "$cache_dir/$incomplete_sha" ]]; then
  printf 'synthetic incomplete selector unexpectedly exists\n' >&2
  exit 1
fi
incomplete_tree="$cache_dir/$incomplete_sha/projects/$first_id"
mkdir -p "$incomplete_tree/.benheim"
printf 'assembly_sha256=%s\n' "$incomplete_sha" > "$incomplete_tree/.benheim/manifest"
touch "$incomplete_tree/.benheim/COMPLETE"
incomplete_out="$test_root/diff-incomplete.out"
incomplete_err="$test_root/diff-incomplete.err"
if run_diff "$incomplete_out" "$incomplete_err" "$incomplete_sha" "$second_sha" --type Game.Character; then
  printf 'incomplete cached version unexpectedly succeeded\n' >&2
  exit 1
else
  incomplete_status=$?
fi
[[ "$incomplete_status" == 2 ]]
grep -Fq 'unavailable or incomplete' "$incomplete_err"
printf 'assembly-fail\n' > "$assembly"
failed_out="$test_root/failed.out"
failed_err="$test_root/failed.err"
if run_ensure fail "$failed_out" "$failed_err"; then
  printf 'failed ILSpy output unexpectedly succeeded\n' >&2
  exit 1
fi
failed_sha="$(awk -F= '/ensure-valheim-source: sha256=/{print $2; exit}' "$failed_err")"
assert_no_published_tree "$failed_sha" "$first_id"
grep -Fq 'ILSpy failed to create a complete project tree' "$failed_err"
printf 'assembly-empty\n' > "$assembly"
empty_out="$test_root/empty.out"
empty_err="$test_root/empty.err"
if run_ensure empty "$empty_out" "$empty_err"; then
  printf 'empty metadata unexpectedly succeeded\n' >&2
  exit 1
fi
empty_sha="$(awk -F= '/ensure-valheim-source: sha256=/{print $2; exit}' "$empty_err")"
assert_no_published_tree "$empty_sha" "$first_id"
grep -Fq 'ILSpy returned empty class/interface/struct/delegate/enum metadata' "$empty_err"
printf 'assembly-incomplete-csproj\n' > "$assembly"
incomplete_project_out="$test_root/incomplete-project.out"
incomplete_project_err="$test_root/incomplete-project.err"
if run_ensure incomplete-csproj "$incomplete_project_out" "$incomplete_project_err"; then
  printf 'structurally incomplete project unexpectedly succeeded\n' >&2
  exit 1
fi
incomplete_project_sha="$(awk -F= '/ensure-valheim-source: sha256=/{print $2; exit}' "$incomplete_project_err")"
assert_no_published_tree "$incomplete_project_sha" "$first_id"
grep -Fq 'ILSpy returned incomplete project output' "$incomplete_project_err"
printf 'assembly-race-v1\n' > "$assembly"
: > "$invoke_log"
race_out="$test_root/race.out"
race_err="$test_root/race.err"
FAKE_ILSPY_SLEEP_SECONDS=1 run_ensure slow "$race_out" "$race_err" &
race_pid=$!
for _ in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20; do
  if grep -Fq 'slow project' "$invoke_log"; then
    break
  fi
  sleep 0.05
done
grep -Fq 'slow project' "$invoke_log"
race_sha="$(awk -F= '/ensure-valheim-source: sha256=/{print $2; exit}' "$race_err")"
[[ "$race_sha" =~ ^[0-9a-f]{64}$ ]]
printf 'assembly-race-v2\n' > "$assembly"
race_status=0
wait "$race_pid" || race_status=$?
[[ "$race_status" != 0 ]]
[[ ! -s "$race_out" ]]
assert_no_published_tree "$race_sha" "$first_id"
grep -Fq 'Valheim assembly changed while ILSpy was reading it' "$race_err"
printf 'assembly-lock\n' > "$assembly"
: > "$invoke_log"
lock_a_out="$test_root/lock-a.out"
lock_a_err="$test_root/lock-a.err"
lock_b_out="$test_root/lock-b.out"
lock_b_err="$test_root/lock-b.err"
FAKE_ILSPY_SLEEP_SECONDS=1 run_ensure slow "$lock_a_out" "$lock_a_err" &
lock_a_pid=$!
FAKE_ILSPY_SLEEP_SECONDS=1 run_ensure slow "$lock_b_out" "$lock_b_err" &
lock_b_pid=$!
lock_a_status=0
wait "$lock_a_pid" || lock_a_status=$?
lock_b_status=0
wait "$lock_b_pid" || lock_b_status=$?
[[ "$lock_a_status" == 0 ]]
[[ "$lock_b_status" == 0 ]]
cmp -s "$lock_a_out" "$lock_b_out"
[[ "$(awk '$2 == "project" { count++ } END { print count + 0 }' "$invoke_log")" == 1 ]]
[[ "$(awk '$2 == "types" { count++ } END { print count + 0 }' "$invoke_log")" == 1 ]]
grep -Eq 'ensure-valheim-source: cache=(miss|hit-after-lock)' "$lock_a_err"
grep -Eq 'ensure-valheim-source: cache=(miss|hit-after-lock)' "$lock_b_err"
grep -Fq 'cache=hit-after-lock' "$lock_a_err" "$lock_b_err"
old_id="$first_id"
printf '# launcher identity changed for cache invalidation\n' >> "$fake_ilspy"
identity_out="$test_root/identity.out"
identity_err="$test_root/identity.err"
run_ensure success "$identity_out" "$identity_err"
identity_tree="$(cat "$identity_out")"
identity_id="$(awk -F= '/ensure-valheim-source: decompiler=/{print $2; exit}' "$identity_err")"
[[ "$identity_id" != "$old_id" ]]
[[ "$identity_tree" != "$first_tree" ]]
[[ -d "$first_tree" ]]
[[ -f "$first_tree/.benheim/COMPLETE" ]]
grep -Fqx 'ensure-valheim-source: cache=miss' "$identity_err"
printf 'cached Valheim source tool checks passed\n'
