# George contextual speech sandbox

The existing resident awareness sends `OnResidentApproach`. The
[contextual speech recipe](../recipe-templates/greydwarf-resident-contextual-speech/code.cs)
waits for a clear sightline, snapshots immediate game context, and sends it to
this loopback bridge. The bridge calls OpenRouter and returns one brief remark
or silence. Native `Chat.SetNpcText` displays the remark over George.
Animation and look reactions run independently of the request.

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
registry, or ledger. `/health` reports model, readiness and call count.
Stop the bridge with Ctrl-C when the experiment ends. It is not a background
service and does not start with the game.

Copy the contextual recipe folder into the authorized Lab's runtime registry,
then use `run_recipes` with `greydwarf-resident-contextual-speech`, preset 0.
Walk beyond the awareness radius for eight seconds, then approach with a clear
sightline. Standing nearby does not generate a stream of requests. Speech has
a separate 45-second cooldown, including silent or failed calls.

## Context and limits

The recipe queries day period, native weather name, biome, player Wet/Cold
status effects, whether the tub's native fuel-only Smelter is active, and whether the
player is sitting. It sends no identity, coordinates, inventory, chat, or
private lore. Up to three displayed remarks stay in memory to discourage
repetition. [george.txt](george.txt) owns the experimental character voice.

The model is `google/gemini-2.5-flash-lite`, selected for short interactive
remarks. The bridge permits one call at a time, no queued calls or retries,
an eight-second provider deadline and at most 100 calls per process.
The game has a nine-second network timeout and a twelve-second approach expiry.
Leaving range, losing sight, dying, teleporting, or removing the recipe cancels
the pending reply. Provider errors and invalid or oversized output become
silence. TMP tags and control characters are rejected. A returned line is at
most 140 characters and remains visible for ten seconds.

Midday or weather overrides in other recipes affect what this recipe queries.
This recipe does not change them. Silence and occasional repeated phrasing are
model choices; local validation does not establish dialogue quality. There is
no persistence, multiplayer protocol, conversation memory, or emote coupling.

Run the bridge proof with:

```sh
safe node --test tools/valheim-dev/contextual-speech/bridge.test.mjs
```

Compile the recipe against the current Lab descriptor and check its live
result through Computer Use before treating a new variant as player-visible
proof. The generic [Valheim Dev verification](../PROMPT.md) owns bridge/plugin
changes; this experiment does not alter that runtime.
