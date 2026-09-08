# Valheim Dev Technical Contract

Valheim Dev runs trusted C# inside one disposable local Valheim world. The MCP
server compiles source against the assemblies loaded by the exact game process.
The in-process bridge runs the compiled entrypoint on Unity's main thread.

This document owns the technical contract. [PRODUCT.md](PRODUCT.md) owns the
player promise. [PROMPT.md](PROMPT.md) owns development and registration.
[COMMON_OPERATIONS.md](COMMON_OPERATIONS.md) is the usage reference.

## One World Owns Installed Code

Ben enables Lab after entering a local single-player world. The bridge captures
the network object, scene object, and world ID. It creates a fresh session ID
and loopback listener. It then writes a session descriptor with the exact
Valheim and Valheim Dev bridge build identities.

One active session owns the listener, descriptor, captured world, and build
identity. Startup publishes that session only after every resource is ready. A
failed startup closes the listener, removes the descriptor, and publishes no
authorization.

Every bridge request carries protocol version 4 and the current session ID.
The bridge rejects an old session and rechecks the captured world before it
loads or runs code. Player respawn can replace the local `Player` object without
changing the captured world.

`bh lab off` closes access. It stops the listener, deletes the descriptor, and
cancels queued work. It does not run installed cleanup functions. Re-enabling
Lab in the same world creates a new session and exposes the same installed-code
registry.

Leaving the world, loading a different world, or tearing down the plugin ends
tracking for the current world. The bridge then attempts to clean installed
code and clears that world's registry.
Installed code must not enter another world. One-time game effects, such as
items or damage, are outside that cleanup promise.

The bridge accepts only loopback connections. It rejects work when any of these
conditions apply:

- Valheim is a dedicated or open server.
- A peer is connected.
- Server RPC state is present.
- The local process does not own the player.

Benheim presence, absence, and gameplay health are not authorization inputs.

## Six General Tools

The official TypeScript MCP SDK owns stdio framing, initialization, discovery,
input validation, and result envelopes. Valheim Dev exposes six tools:

1. `lab_status({})`
   - Reports connection and authorization state, exact build identity,
     `restart_required`, and installed code.
2. `run_once({label, source, targets?, inputs?, evidence_events?, evidence_timeout_ms?})`
   - Compiles `public static ValheimDevCommand.Run(string inputJson): string`.
   - Runs once for observation or action.
   - Uses `cleanup_state: not_applicable` and never claims to reverse effects.
3. `install_change({label, change_id, source, targets?, inputs?, evidence_events?, evidence_timeout_ms?})`
   - Compiles `public static ValheimDevChange.Run(string inputJson): string` and
     `public static ValheimDevChange.Cleanup(): void`.
   - Installs new ongoing code or replaces the same `change_id`.
4. `remove_change({label, change_id})`
   - Calls the installed cleanup entrypoint.
   - Removes the registry entry only after cleanup succeeds.
5. `run_recipes({recipes: [{id, preset_index?, inputs?}, ...]})`
   - Reads each `registry/<id>/code.cs` when that recipe is reached.
   - Accepts an optional zero-based preset index from `presets.json` or optional
     ad hoc structured inputs. Omit both to run without supplied inputs. Do not
     provide both.
   - Uses `ValheimDevCommand` for a one-time run and `ValheimDevChange` for a
     managed change whose `change_id` is the recipe ID.
   - Runs recipes in request order and returns one outcome for every requested
     recipe.
   - Continues after a safe per-recipe failure. It does not attempt later
     recipes after `runtime_unresolved` or `restart_required` makes mutation
     safety uncertain.
6. `read_ledger({operation_id?, limit?})`
   - Returns either a compact newest-first history or one run's compact summary
     and complete details, including warnings or errors observed since that run
     started.
   - Works without an active Lab connection.

`label` is a short human description used in history. `source` is exact UTF-8
C# and is limited to 256 KiB. A compiled assembly is limited to 1 MiB. A change
ID contains 1 to 128 ASCII letters, digits, dots, underscores, or hyphens. A
recipe ID uses the same characters and must start with a letter or digit. Each
`code.cs` and `presets.json` file is limited to 256 KiB. Every schema rejects
extra top-level fields.

`targets` is an optional JSON object or array recorded as operation context in
the ledger. The bridge does not interpret it as selectors, handles, or
permissions.

`inputs` is an optional JSON object or array. The MCP server serializes it and
passes it to `Run(string inputJson)`. `Run` must return a serialized JSON object
or array. The runtime rejects a plain-text or malformed result. The MCP server
parses the result back into structured JSON for the normal response. This lets
one run feed the next without source rewriting or prose parsing. `run_recipes`
passes the selected preset or ad hoc inputs through this same contract.

`evidence_events` selects up to 64 Developer Diagnostics events by
`Domain:event`. An operation can wait up to 120 seconds for selected evidence.
The response labels selected evidence as non-exhaustive and reports count or
byte truncation. Feature-specific evidence is an optional integration with the
mod that owns those events. If its provider is absent or unhealthy, the
operation continues and reports the requested evidence as unavailable.

## Compilation Matches The Running Process

The session descriptor lists every unique assembly that is loaded in Valheim's
application domain and backed by a file. It skips dynamic and in-memory
assemblies. The list is sorted so the descriptor remains deterministic.

At startup, Valheim Dev requires compiler references for the core runtime,
Valheim, the Valheim Dev bridge, BepInEx, and Harmony. Benheim is not required.
When another mod is loaded, its assembly can appear in the normal loaded-
assembly list so experimental code may call a helper that mod deliberately
exposes. Loaded Unity modules and game libraries do not need a feature-specific
allowlist. A type that is not loaded can be located through the current
decompiled source and game files before the agent writes the command.

The MCP server invokes Roslyn directly with `-nostdlib+`. Source compilation and
ledger I/O happen outside Unity's main thread. Temporary source and assemblies
use owner-only directories and are deleted after each operation.

## Installed Changes Keep One Working Version

Each `change_id` owns at most one installed version. The bridge records the
operation ID, source and assembly hashes, install time, result, and cleanup
state with its loaded entrypoints.

Each install or removal request includes the observed operation ID of the prior
version. It includes explicit `null` when no prior version existed. The runtime
compares this value on the main thread immediately before mutation. A mismatch
returns `stale_change_state` without running cleanup or new code.

Replacement follows this order:

1. Compile and validate the candidate before touching the installed version.
2. Clean the installed version immediately before running the candidate.
3. Run the candidate. Register it only after `Run()` succeeds.
4. If `Run()` fails, clean the candidate and run the prior version again.
5. Report preservation only after that restoration succeeds.

A successful install is `active`. A successful removal is `cleaned`. A failed
candidate that restores the prior version is `restored` for that operation.

If cleanup or restoration is uncertain, the bridge sets `restart_required`.
It keeps the uncertain registry entry visible and refuses every code operation.
Status remains available so Codex can explain the state. Ben decides whether
to restart Valheim.

## Responses Stay Small; The Ledger Keeps Detail

A normal response from `run_once`, `install_change`, or `remove_change`
contains its state, operation ID, result or error, and relevant installed-state
changes. A `run_recipes` response contains one outcome for every requested
recipe in request order. A recipe that reaches a code operation includes its
operation summary. A recipe that fails before execution or is skipped after
runtime uncertainty reports its recipe metadata, state, null operation ID, and
error. Skipped recipes use `state: not_attempted`. The response also includes
the final active changes and restart state. Evidence appears only when the
request selected evidence. Source, hashes, compiler output, timestamps, and
full history stay in the ledger.

`lab_status` is the live installed-code inventory. Registration does not prove
that an effect is still visible. The agent must observe the current runtime or
ask Ben when that distinction matters.

The ledger writes a pending record before compilation, then atomically updates
that record. Schema version 4 stores the label, duration, exact source and build
identity, compiler and runtime outcomes, installed-state snapshots, selected
evidence, cleanup state, and a cursor into the existing BepInEx log. Records use
owner-only permissions.

History lists compact run summaries with label, time, outcome, duration,
structured result, and error. Reading one run returns its compact summary and
complete ledger record. `read_ledger` also reads the current BepInEx log from
that run's saved cursor to the present. It keeps warnings and errors and
collapses identical messages into counts. This lets an agent see failures from
ongoing callbacks after `Run` returned. Log association is temporal only; it
does not claim that the run caused a message. Missing, replaced, oversized, or
unavailable logs are reported explicitly.

`runtime_unresolved` means transport stopped waiting after runtime execution
may have started. It is not terminal. Codex must read status and the ledger
before another mutation.

## Unity Keeps Control Of Its Main Thread

The bridge parses sockets and queues work away from Unity's main thread.
Unity's update loop runs one operation at a time. Evidence waiting yields across
frames. Mutations are serialized, so status never exposes a half-committed
registry change.

Runtime entrypoints must return. Valheim Dev cannot preempt C# that hangs the
main thread. `ValheimDevCancellation.IsCancellationRequested` becomes true when
the tracked world ends, not when Ben only turns Lab access off.
