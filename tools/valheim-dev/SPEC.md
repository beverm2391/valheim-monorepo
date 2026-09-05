# Valheim Dev Technical Contract

Valheim Dev connects Codex to one authorized Valheim process for one Lab
session. The MCP server compiles trusted C# against the exact running
build. The in-process bridge executes that code on Unity's main thread. Every
operation records enough identity and outcome data to explain what ran later.

This document owns the technical contract. [PRODUCT.md](PRODUCT.md) owns the
player promise. [PROMPT.md](PROMPT.md) owns local development and registration.

This contract covers the complete workbench. The current implementation can
report status, inspect the runtime, install, replace, or remove managed changes,
and read the ledger. It does not yet expose watches, native target handles, or
one-time invocation. Those required extensions must follow this contract.

## A Session Is The Authority Boundary

Ben authorizes one loaded single-player Lab world with `bh lab on`. The bridge
captures the world identity and creates a fresh session ID, generation, token,
and loopback port. It also records exact Valheim and Benheim versions and
SHA-256 hashes. The descriptor supplies an explicit compiler reference set for
those builds.

Every bridge request includes the protocol version, token, and generation. The
bridge compares the token in constant time and rejects stale generations. It
also rechecks the captured world before loading code and before calling its
entrypoint. A local player replacement after respawn does not change the world
identity.

The authorization ends when Ben runs `bh lab off`, the captured world changes,
the network session ends, or the plugin stops. The bridge then deletes the
descriptor, rejects queued work, stops watches, and attempts to clean every
managed change. A new authorization always creates a new token and generation.

Valheim Dev accepts only loopback connections. It rejects a request in any of
these conditions:

- Valheim is a dedicated or open server.
- A peer is connected.
- Server RPC state is present.
- Benheim gameplay hooks are unhealthy.
- The local process does not own the player.

## The MCP Surface

All operation IDs are UUIDs created by the MCP server. Caller-supplied
`change_id` and `watch_id` values must contain 1 to 128 characters. Each
character must be a letter, digit, dot, underscore, or hyphen. Every schema must
reject extra top-level fields.

The implemented core tools are:

1. `lab_status({})`
   - Returns connection state, authorization state, session and generation,
     exact build identity, `restart_required`, and active changes. The complete
     extension also returns active watches and an in-flight operation.
2. `inspect_runtime({source, targets?, inputs?, evidence_events?, evidence_timeout_ms?})`
   - Compiles source that defines `public static ValheimDevInspection.Run():
     string`.
   - Runs once without installing a managed change.
   - Returns the inspection result, selected evidence, and current active state.
3. `install_change({change_id, source, targets?, inputs?, evidence_events?, evidence_timeout_ms?})`
   - Compiles source that defines `public static ValheimDevChange.Run(): string`
     and `public static ValheimDevChange.Cleanup(): void`.
   - Installs a new managed change or replaces the same `change_id`.
4. `remove_change({change_id})`
   - Calls the installed cleanup entrypoint.
   - Removes the registry entry only after cleanup succeeds.
5. `read_ledger({operation_id?, limit?})`
   - Returns one exact record or a newest-first bounded list.
   - Works without a live Lab connection.

The complete workbench also requires these tools:

6. `start_watch({watch_id, source, targets?, inputs?, event_selectors?, max_events?, max_bytes?})`
   - Compiles `public static ValheimDevWatch.Start(): string` and
     `public static ValheimDevWatch.Stop(): void`.
   - Installs one bounded watcher until replacement, removal, or revocation.
7. `capture_watch({watch_id, cursor?, wait_ms?})`
   - Returns selected observations after the cursor, the next cursor, dropped
     event counts, truncation state, and the watch's active state.
8. `stop_watch({watch_id})`
   - Calls `Stop()` and removes the watcher only after cleanup succeeds.
9. `invoke_once({source, targets?, inputs?, evidence_events?, evidence_timeout_ms?, impact, acknowledge_not_reversible})`
   - Compiles `public static ValheimDevInvocation.Run(): string`.
   - Requires `impact` to describe the world effect and
     `acknowledge_not_reversible` to equal `true`.
   - Records a one-time action. It never reports that the action was removed.

`event_selectors` follows the same rules as `evidence_events`. `max_events`
defaults to 512 and cannot exceed 4,096. `max_bytes` defaults to 1 MiB and cannot
exceed 4 MiB. A capture cursor is a non-negative integer. `wait_ms` defaults to
zero and cannot exceed 120 seconds. `impact` is a non-empty string of at most
1,024 characters. Every operation response includes its action, operation ID,
session and generation, build identity, timestamps, result or error, cleanup
state, restart state, selected evidence, and active registry snapshot.

`source` is exact UTF-8 C# source and is limited to 256 KiB. Compiled assemblies
are limited to 1 MiB. `targets` and `inputs` are JSON values copied unchanged to
the ledger. Evidence selectors use `Domain:event`, with at most 64 selectors.
An operation can wait up to 120 seconds for selected evidence. Selected
evidence remains explicitly non-exhaustive.

The current MCP accepts either an object or an array for each of `targets` and
`inputs`. When native selectors and handles are implemented, `targets` will
accept only the structures below. This change will not alter the meaning of
target data in the ledger.

## Targets Point At Live Objects

A target reference is either a selector or a session-local handle:

```json
{
  "selector": {
    "source": "local_player | crosshair | hovered_ui | scene",
    "name": "optional exact or partial name",
    "type": "optional full type name",
    "component": "optional full component type",
    "path": "optional hierarchy path",
    "max_results": 20
  }
}
```

```json
{ "handle": "vh:<generation>:<opaque-id>" }
```

A selector must be bounded. Scene searches require at least one of `name`,
`type`, `component`, or `path`. `max_results` defaults to 20 and cannot exceed
100. The runtime returns stable handles for the selected objects within the
current generation.

A handle is a registry reference, not a Unity instance ID. The registry keeps
the live object reference plus its observed type, name, hierarchy path, and
generation. A handle expires when the object is destroyed, the world changes,
or authorization ends. The bridge rejects a handle from another generation.
Handles never enter shipped source or survive a process restart.

## Inspection Describes What Actually Exists

Inspection can return object identity, hierarchy, components, fields,
properties, and available methods. It defaults to public instance members.
Private members, static members, or method signatures require an explicit
request in the inspection source. Inspection must bound traversal depth,
collection length, string length, and total response bytes.

Value serialization preserves JSON primitives directly. Unity objects become
target summaries with a handle. Enums include their symbolic name and numeric
value. Vectors, colors, bounds, quaternions, and transforms use named numeric
fields. Collections are bounded and report truncation. For an unknown object,
serialization returns its type and a bounded string representation. If a getter
throws, inspection records a member error without failing the full inspection.

Inspection must not claim to be side-effect-free merely because it reads a
property. Caller-supplied inspection code is trusted C# and can invoke arbitrary
runtime behavior. Codex must use inspection entrypoints only for observation.

## Managed Changes Keep One Working Version

Each `change_id` owns at most one active version. The bridge records the
operation ID, source and assembly hashes, install time, result, and cleanup
state with the loaded entrypoints.

Every install and removal carries the operation ID observed for the prior
version, or explicit `null` when the ID was absent. The runtime compares that
expectation on the main thread immediately before mutation. A mismatch returns
`stale_change_state` without cleanup or installation; the caller must inspect
fresh status and decide again.

Installation follows this order:

1. Compile and validate the candidate before touching the active version.
2. If compilation or entrypoint validation fails, re-read the same authorized
   runtime before claiming that the active version is still present. If the
   session changed or cannot be read, preservation is unknown.
3. Clean the active version immediately before running the candidate.
4. Run the candidate and register it only after `Run()` succeeds.
5. If `Run()` fails, clean the candidate and run the prior version again.
6. Report `previous_change_preserved: true` only after that restoration works.

A successful install remains `active`; it does not clean itself after the
evidence window. A successful removal becomes `cleaned`. A failed candidate
that restores the prior version becomes `restored` for that operation while the
prior registry entry remains `active`.

If cleanup or restoration is uncertain, the bridge sets `restart_required`.
It keeps any uncertain registry entry visible and refuses further mutations.
Status and inspection remain available so Codex can explain the state. Ben
decides when to restart Valheim.

## Watches Capture Bounded Evidence

A watch is a managed change whose purpose is observation. Each `watch_id` owns
one active watch and one bounded ring buffer. Replacement follows the same
prepare, stop, start, and restore rules as managed changes.

The watch records only declared event selectors. `max_events` and `max_bytes`
bound memory. When either bound is reached, the buffer drops the oldest event
and increments `dropped_events`. Each event gets a monotonically increasing
cursor within the watch. `capture_watch` can wait cooperatively for new data,
but it cannot block Unity's main thread.

Benheim Developer Diagnostics remains the owner of shipped gameplay events.
Valheim Dev watches may select those events and may add temporary Lab-only
observations. The ledger states which selectors were active and never labels a
bounded capture as exhaustive.

Every current operation response and ledger record includes
`evidence_truncated` and `dropped_evidence_events`. Reaching either the event
count or byte bound makes truncation explicit and increments the dropped count.
The byte bound is the UTF-8 size of the serialized `evidence_events` JSON array,
including its brackets, commas, quoted strings, and escaping.

Visible evidence can also come from Ben's observation or a separate screenshot
capture. The ledger records the selected evidence and its external artifact
reference when one exists. The bridge does not invent visual proof from logs.

## One-Time Invocation Tells The Truth About Cleanup

`invoke_once` is for actions whose effects can outlive the entrypoint. Examples
include spawning, damage, inventory changes, and direct world-changing method
calls. The caller describes the impact and explicitly acknowledges that the
effect is not reversible.

The operation uses `cleanup_state: not_applicable`. Revocation stops new work
but does not claim to undo the completed action. The disposable world is the
recovery boundary.

## Status Is The Live Inventory

`lab_status` reports the descriptor identity and the bridge's current registry.
For each active change or watch, `lab_status` reports its stable ID, the
operation ID that installed it, source and assembly hashes, install time, latest
result, and cleanup state. It also reports an in-flight operation and
`restart_required` when present.

The MCP server validates that the bridge response matches the descriptor's
session, generation, Valheim build, and Benheim build. A mismatch makes the
connection unavailable. The MCP server never merges active state from an old
generation into a new one.

## The Ledger Is Persistent Evidence

The MCP server writes a pending record before compilation. Each operation then
updates the same atomic JSON record. Schema version 2 contains:

- action, operation ID, change or watch ID, session, and generation;
- exact Valheim and Benheim versions and SHA-256 hashes;
- exact source, source hash, assembly hash, targets, and inputs;
- prior active version and whether it was preserved;
- compiler outcome, bounded output, timestamps, and failure reason;
- runtime result, bounded exception, cleanup state, and active registry snapshot;
- requested evidence selectors, selected events, truncation, and artifact references;
- `pending`, `compile_failed`, `runtime_failed`, `runtime_unresolved`, or
  `succeeded`, plus whether the record is terminal.

`runtime_unresolved` is not terminal. It means the transport stopped waiting
after runtime execution may have started. Codex must inspect status and the
ledger before another mutation. A missing connection does not erase ledger
records.

The MCP server writes records with owner-only permissions and atomic rename.
Temporary source and assemblies use owner-only temporary directories and are
deleted after each operation. The ledger is the durable artifact, not the
compiled DLL.

## Main-Thread And Concurrency Rules

Socket parsing, compilation, and ledger I/O happen outside Unity's main thread.
The bridge puts validated requests in a bounded queue. Unity's update loop runs
one operation at a time on the main thread. While an operation waits, Unity's
update loop drains selected evidence between frames.

Runtime entrypoints must return control. Valheim Dev cannot preempt C# that
hangs the main thread. Cancellation is cooperative and becomes visible through
`ValheimDevCancellation.IsCancellationRequested`.

The bridge serializes mutations. It does not install, replace, remove, start,
or stop two managed entries concurrently. A read-only status request can enter
the queue, but its response never includes a half-committed registry update.
Watch callbacks must do bounded work and must copy observations into their
buffer without blocking.

## End-To-End Examples

For the Affinity icon loop, Codex first inspects the hovered inventory or hotbar
icon and records its component tree. It installs `affinity.weapon-icon` with a
small animation variant. Ben watches the icon. Codex captures selected
diagnostics, replaces the same change ID with another variant, and removes the
change after Ben chooses a direction. The chosen behavior enters normal Benheim
source and a normal package later.

If the second variant does not compile, the bridge never receives it. The
ledger records `compile_failed` and re-reads the same authorization. It records
the prior active summary and `previous_change_preserved: true` only when that
runtime still reports the first variant; otherwise both preservation and the
current active inventory are unknown.

If Codex spawns a creature in a disposable test, it uses `invoke_once`,
describes the impact, and sets `acknowledge_not_reversible` to `true`. The
ledger records `cleanup_state: not_applicable`. Valheim Dev cannot remove the
effect; resetting the disposable world can.
