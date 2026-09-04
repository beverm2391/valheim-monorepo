# Valheim Dev

Valheim Dev is an agent-only tool that connects Codex to an explicit local,
single-player Lab session through the `valheim-dev` stdio MCP server. Codex
uses it to inspect and change that session while Ben plays in Valheim. It lets
Codex compare experiments without a new package, installation, or Valheim
relaunch, and each experiment leaves durable evidence. Ben continues to use
Valheim and its native console; Valheim Dev has no human-facing CLI or
dashboard.

## One Live Experiment Loop

Ben alone creates, selects, deletes, restores, and resets one disposable local
test character and one new disposable local test world through Valheim. Both
remain outside the repository. After Ben enters that world and starts the
explicit local, single-player Lab session, Valheim Dev connects only to that
session. Valheim Dev has no save-management authority or tool.

After Ben enters the disposable test world, he runs `bh lab on` in Valheim's
console. This grants Lab authorization for that world session. While
authorization is active, Codex may apply repeated experiments without Ben's
approval for each operation. Running `bh lab off`, leaving the world, or
quitting Valheim revokes the authorization immediately. Authorization applies
only to the current world session and never persists. Valheim Dev cannot enable
Lab mode or grant authorization.

The first agent interface must support these outcomes:

- Confirm the connected Lab session and its Valheim and Benheim builds.
- Apply a self-contained C# experiment without creating a new Benheim package,
  installing it, or relaunching Valheim.
- Read the experiment's persistent result and evidence.

The agent submits the experiment through one apply operation. Compilation and
loading happen inside that operation and are not part of the product
interaction. The experiment contains any required inspection, changes, and
measurements, and Valheim Dev runs it on Unity's main thread.

## Every Operation Leaves a Persistent Ledger Record

Each Lab session has a persistent operation ledger. For each operation, it
records the session and operation identities, the exact source that ran, and a
hash for that source or artifact. It also records the connected Valheim and
Benheim build identities, targets, inputs, timestamps, result or exception,
and cleanup state.

Each experiment may record selected state before and after its operation,
selected transitions during it, or existing typed Benheim events. The ledger
links each observation to the operation that produced it. If the ledger records
only selected effects, Valheim Dev must say so. It must not imply that the
record includes every effect that followed the operation.

The agent can read the ledger during the Lab session and after Valheim exits.
The record must identify the exact experiment that ran and explain a failure.

## The Lab State Is Disposable

The first Lab session may run unrestricted experiment code. Valheim Dev may
attach only to that session. Valheim Dev must never attach to an ordinary
Benheim session, the shared production world, or a dedicated server.

An experiment may provide cleanup, but Valheim Dev does not promise that
arbitrary runtime code can undo its changes. Restarting Valheim clears runtime
patches, callbacks, coroutines, static state, and loaded objects. Deleting and
recreating the local test world and character resets persistent state.

Valheim Dev has no tool or authority to launch, quit, or restart Valheim. If an
experiment leaves Valheim in a state that requires a restart, Valheim Dev
reports the `restart_required` status. Ben decides when to restart Valheim.

## Relationship To Developer Diagnostics

[Benheim Developer Diagnostics](../../client-mods/benheim/src/DeveloperDiagnostics/PRODUCT.md)
owns typed events produced by shipped gameplay features during normal play.
Valheim Dev may observe and correlate those events, but it does not replace the
schema, Axiom delivery, probe registry, or in-game console controls that
Developer Diagnostics owns.

Valheim Dev owns the connection to the Lab session, the source and selected
evidence for each applied experiment, and the persistent operation ledger.

## First Proof

The first proof is one live Lunge tuning session. The agent must confirm the Lab
session and the Valheim and Benheim builds. It must apply one self-contained C#
experiment that inspects the relevant Valheim and Benheim integration points
and records the resulting velocity and movement state. It must read the
persistent result and evidence, then apply a second variation while Ben remains
in the Lab session. The ledger must preserve both experiments and their recorded
results.

This slice succeeds when Ben can compare two Lunge variants by their recorded
velocity and movement state without creating a new Benheim package, installing
it, or relaunching Valheim between variants. These capabilities remain deferred
until this loop proves its value:

- Generic tools for object discovery, reading, writing, and invoking methods.
- Reusable watchers and broader tracing.
- Fixture-building tools, remote multiplayer control, Axiom delivery, and a
  visual debugger.
