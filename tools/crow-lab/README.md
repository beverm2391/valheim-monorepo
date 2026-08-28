# Crow lab

Crow lab sends a short Valheim event timeline to one OpenRouter model. It can
also compare the same raw prompt across a fixed list of models. The public
files contain the runner, required response format, public base prompt, schema,
and synthetic scenarios.
Personal lore, private examples, and model results stay local.

The runner works with only `prompts/base.md`. It also loads
`prompts/player_lore.md` and `prompts/examples.md` when those ignored files
exist and contain text.

## Set up private context

The lab requires Node.js 18 or later, TypeScript, and `ts-node`. Copy the empty
templates before adding context that you own:

```bash
cp tools/crow-lab/prompts/player_lore.example.md \
  tools/crow-lab/prompts/player_lore.md
cp tools/crow-lab/prompts/examples.example.md \
  tools/crow-lab/prompts/examples.md
```

Both destination files are ignored. Keep other private prompt material in
`tools/crow-lab/private/`. Store generated or raw model output in
`tools/crow-lab/results/`, which is also ignored.

## Run a scenario

Check the public prompt and scenario path without making a network request:

```bash
ts-node --transpile-only --project tools/crow-lab/tsconfig.json \
  tools/crow-lab/crow.ts tools/crow-lab/scenarios/meaningful-kill.json \
  --model <openrouter-model-id> --dry-run
```

For a live run, supply `OPENROUTER_API_KEY` through a scoped secret manager.
Do not put the key in this repository or a prompt file.

```bash
your-secret-manager run -- ts-node --transpile-only \
  --project tools/crow-lab/tsconfig.json \
  tools/crow-lab/crow.ts tools/crow-lab/scenarios/meaningful-kill.json \
  --model <openrouter-model-id>
```

The comparator accepts either `--prompt` or `--prompt-file` and prints each
model response to standard output. When model output contains private material,
redirect it only to the ignored `tools/crow-lab/results/` directory:

```bash
your-secret-manager run -- ts-node --transpile-only \
  --project tools/crow-lab/tsconfig.json \
  tools/crow-lab/compare.ts --prompt-file <path-to-prompt>
```

Model availability and identifiers can change. Update the model list in
`compare.ts` when OpenRouter no longer offers a listed model.

## Scenario contract

Each scenario is one JSON object with a non-empty `timeline`. Every timeline
entry needs an ISO timestamp and one of these kinds:

- `global_chat`
- `crow_message`
- `gameplay_event`

Exactly one gameplay event must set `trigger` to `true`. The runner enforces
the single-trigger rule. `scenario.schema.json` documents the JSON structure.

Crow returns exactly `{"speak": boolean, "text": string}`. When `speak` is
false, `text` must be empty. When `speak` is true, `text` must contain a
non-empty message.
