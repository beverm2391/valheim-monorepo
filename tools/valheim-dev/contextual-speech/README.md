# George contextual speech sandbox

The existing resident awareness sends `OnResidentApproach`. The
[contextual speech recipe](../recipe-templates/greydwarf-resident-contextual-speech/code.cs)
waits for a clear sightline, snapshots immediate game context, and sends it to
this loopback bridge. The bridge calls OpenRouter and returns one brief remark
or silence. Native `Chat.SetNpcText` displays the remark over George.
Animation and look reactions run independently of the request.
After native display succeeds, the optional `OnResidentSpeechShown(Player)`
callback gives the awareness receiver its addressee. Callback failure is
recorded separately and cannot turn displayed speech into silence.

This is a disposable Lab experiment, not an installed Benheim feature.
[Recipe dependencies](../recipe-templates/README.md) own resident setup.
The contextual recipe replaces the fixed speech receiver only; it does not
spawn, reposition, animate, or reserve seats. Remove the fixed speech change
before attaching the contextual receiver. Remove contextual speech before
removing the resident.

## Run

Use Node 22 or later. Inject only `OPENROUTER_API_KEY` into the bridge process
through the operator's scoped secret workflow. For example:

```sh
safe b secrets run --project PROJECT --config CONFIG --secret OPENROUTER_API_KEY -- \
  node tools/valheim-dev/contextual-speech/bridge.mjs
```

The process listens only on `127.0.0.1:18741`. `GEORGE_SPEECH_PORT` overrides
the port; pass the same `port` to the recipe. No key enters the recipe, game,
registry, or ledger. `/health` reports model, readiness, call count and the
current bridge trace file.
Stop the bridge with Ctrl-C when the experiment ends. It is not a background
service and does not start with the game.

Copy the contextual recipe folder into the authorized Lab's runtime registry,
then use `run_recipes` with `greydwarf-resident-contextual-speech`, preset 0.
Walk beyond the awareness radius for eight seconds, then approach with a clear
sightline. Standing nearby does not generate a stream of requests. Speech has
a separate 45-second cooldown, including silent or failed calls.

For paired dialogue iteration, send `OnLabDialogueTest` to the resident with
`Player.m_localPlayer` through a one-time Lab command. This uses the same
context, provider request, and native display, bypassing approach cooldown,
range, and sightline. Death, teleport, expiry, and receiver removal still
cancel the reply. The bridge reads [george.txt](george.txt) once per request,
so an edited prompt reaches the next preview without restarting the bridge;
that same snapshot is saved in the trace.

## Context and limits

The recipe queries day period, native weather name, biome, player Wet/Cold
status effects, whether the tub's native fuel-only Smelter is active, and whether the
player is sitting, plus the visitor's current character name and whether the
event was an approach or explicit dialogue preview. It sends no account IDs,
coordinates, inventory, chat, or
private lore. Up to three displayed remarks stay in memory to discourage
repetition. [george.txt](george.txt) owns the experimental character voice.

The [bridge's `MODEL` constant](bridge.mjs) selects the Gemini Flash Lite model
for short interactive remarks. The bridge permits one call at a time, no queued
calls or retries,
an eight-second provider deadline and at most 100 calls per process.
The game has a nine-second network timeout and a twelve-second approach expiry.
Leaving range, losing sight, dying, teleporting, or removing the recipe cancels
the pending reply. Provider errors and invalid or oversized output become
silence. TMP tags and control characters are rejected. A returned line is at
most 140 characters and remains visible for ten seconds.

Midday or weather overrides in other recipes affect what this recipe queries.
This recipe does not change them. Silence and occasional repeated phrasing are
model choices; local validation does not establish dialogue quality. There is
no persistent conversation memory, multiplayer protocol, or emote coupling.

## Debug a remark

Each approach receives a UUID shared by the game and bridge. Both append local
JSONL records immediately, so they remain readable while the secret wrapper
buffers stdout. Routine console logs contain IDs, outcomes and counters;
dialogue lives in the local trace files.

The bridge writes to the ignored [traces directory](traces/.gitignore), or
`GEORGE_TRACE_DIR` when set. `/health` gives its exact file and `traceError`.
The game writes below the active profile's
`BepInEx/ValheimDev/contextual-speech-traces`; the receiver's `traceFile` field
gives its exact path. Each process/receiver gets a new file. Traces remain
until the operator removes them; they are local debugging data, not repo
artifacts to publish.

Read both files, optionally filtering one approach ID:

```sh
safe node tools/valheim-dev/contextual-speech/trace.mjs \
  --request-id UUID /absolute/path/to/bridge.jsonl /absolute/path/to/game.jsonl
```

The bridge records the exact prompt and approved context sent to OpenRouter,
bounded model content, parsed reply, actual model, token usage, cost when
reported, and elapsed time. Provider HTTP errors retain only the status;
authorization headers, API keys and raw provider error bodies never enter
the trace. Unknown request fields are rejected before persistence. Game
`contextJson` and `replyJson` hold the snapshot and validated response.

`provider_completed` means a provider response arrived. `bridge_response`
means the bridge submitted a loopback response. Game `reply_received` means
the receiver accepted it; `display_submitted` means native `SetNpcText`
returned successfully. Only a live visual check establishes that the player
saw the text. Suppression, silence and discards carry separate reasons,
including cooldown, missing context, sightline timeout, visitor departure,
provider timeout, invalid output and receiver removal. Cancellation is
recorded immediately; a late provider completion remains linked to the same
ID and cannot become delivered speech. Trace write failures are surfaced once
in routine logs and remain queryable through `/health` or `traceFailures`.

## Offline proof

Run the bridge proof with:

```sh
safe node --test tools/valheim-dev/contextual-speech/bridge.test.mjs
```

Compile the recipe without launching or installing anything:

```sh
safe VALHEIM_GAME_DIR=/absolute/path/to/Valheim \
  node tools/valheim-dev/contextual-speech/compile-recipe.mjs
```

The controlled transport proof uses no paid calls. Compilation checks the
installed game APIs; the next authorized live session must check matching
game/bridge IDs, native submission and cancellation through Computer Use.
The generic [Valheim Dev verification](../PROMPT.md) owns bridge/plugin
changes; this experiment does not alter that runtime.
