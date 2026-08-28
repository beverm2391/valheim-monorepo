#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

public_files=(
  tools/crow-lab/README.md
  tools/crow-lab/compare.ts
  tools/crow-lab/crow.ts
  tools/crow-lab/prompts/base.md
  tools/crow-lab/prompts/examples.example.md
  tools/crow-lab/prompts/player_lore.example.md
  tools/crow-lab/scenario.schema.json
  tools/crow-lab/scenarios/boring-silence.json
  tools/crow-lab/scenarios/escaped-critical-health.json
  tools/crow-lab/scenarios/lone-survivor.json
  tools/crow-lab/scenarios/meaningful-kill.json
  tools/crow-lab/scenarios/raid-context.json
  tools/crow-lab/scenarios/repeated-death-same-enemy.json
  tools/crow-lab/tsconfig.json
)

for file in "${public_files[@]}"; do
  git ls-files --error-unmatch "$file" >/dev/null 2>&1 || fail "public file is not tracked: $file"
done

private_paths=(
  tools/crow-lab/prompts/player_lore.md
  tools/crow-lab/prompts/examples.md
  tools/crow-lab/private/writer-room.md
  tools/crow-lab/results/comparison.txt
)

for file in "${private_paths[@]}"; do
  if git ls-files --error-unmatch "$file" >/dev/null 2>&1; then
    fail "private path is tracked: $file"
  fi
  ignore_source="$(git check-ignore -v --no-index "$file" || true)"
  [[ "$ignore_source" == .gitignore:* ]] || fail "private path is not owned by .gitignore: $file"
done

if git ls-files tools/crow-lab | rg -q '(^|/)(private|results|node_modules|dist)/|prompts/(player_lore|examples)\.md$|\.(zip|tgz)$'; then
  fail "tracked lab content includes a private or generated path"
fi

temporary_root="$(mktemp -d)"
trap 'rm -rf "$temporary_root"' EXIT
for file in \
  tools/crow-lab/compare.ts \
  tools/crow-lab/crow.ts \
  tools/crow-lab/tsconfig.json \
  tools/crow-lab/prompts/base.md \
  tools/crow-lab/scenarios/meaningful-kill.json; do
  mkdir -p "$temporary_root/$(dirname "$file")"
  cp "$file" "$temporary_root/$file"
done

test ! -e "$temporary_root/tools/crow-lab/prompts/player_lore.md" || fail "private lore entered the fresh-copy proof"
test ! -e "$temporary_root/tools/crow-lab/prompts/examples.md" || fail "private examples entered the fresh-copy proof"

ts-node --transpile-only --project "$temporary_root/tools/crow-lab/tsconfig.json" \
  "$temporary_root/tools/crow-lab/crow.ts" \
  "$temporary_root/tools/crow-lab/scenarios/meaningful-kill.json" \
  --model example/model --dry-run >/dev/null

ts-node --transpile-only --project tools/crow-lab/tsconfig.json tools/crow-lab/crow.ts \
  --validate-response '{"speak":false,"text":""}' >/dev/null

set +e
comparison_output="$(
  env -u OPENROUTER_API_KEY \
    ts-node --transpile-only --project "$temporary_root/tools/crow-lab/tsconfig.json" \
    "$temporary_root/tools/crow-lab/compare.ts" --prompt smoke 2>&1
)"
comparison_status=$?
set -e
if ((comparison_status == 0)) || [[ "$comparison_output" != *"OPENROUTER_API_KEY is required"* ]]; then
  fail "comparator did not require an external OpenRouter key"
fi

echo "PASS: Crow lab public/private boundary"
