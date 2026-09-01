# Developer Diagnostics

Developer Diagnostics keeps evidence from normal play available so a developer
can explain what a feature did. It uses Benheim's existing typed diagnostics
instead of adding a separate event system, log format, or remote destination.

During normal play, Benheim records the evidence needed to explain important
feature decisions and outcomes without a special debug build. A developer
starts extra inspection only when needed. The inspection stops and cleans up
when it ends.

## Product Direction

Benheim always records gameplay events for important actions, decisions, state
changes, results, and cleanup. These events do not record every frame or repeat
unchanged state. The root [Benheim product](../../PRODUCT.md) defines how typed
events appear in local logs, NDJSON, and Axiom, including the privacy
guarantees.

A probe performs extra work only while a developer is inspecting the game. It
can inspect one loaded Unity object, observe one named point in the game's
behavior for a short time, or show a temporary debug overlay. Each probe must
answer one specific question and limit its duration, output, and effect on
performance.

Developer Diagnostics supports two probe lifecycles:

- A **snapshot** runs once, emits a limited result, and cleans up immediately.
  The runtime UI catalog, effect catalog, and comfort calculation diagnostic
  are snapshots.
- A **watcher** observes one named point in the game's behavior until a
  developer turns it off or the session ends. Collider overlays and short
  render experiments are watchers.

Each watcher ships with a default state. During the current session, a
developer can set the watcher to `on`, `off`, or `default`. Relaunching Benheim
clears this session setting and restores the shipped default. The status view
must show the shipped default, the session setting, and the effective state so
a developer can tell what evidence is active.

## Command Experience

Developer Diagnostics uses short, discoverable commands instead of a deep
`bh debug ...` tree. Valheim's native console completion must show each command
name and the available choices for its first argument.

The command families are:

- `bhcatalog` lists a limited catalog from the running game.
- `bhrun` runs a snapshot once.
- `bhwatch` shows watcher state and changes session settings.

The commands define their exact syntax and available probe names. Through
native completion, a player can discover the commands, see which watchers are
active, and restore a watcher to its shipped default without relaunching.

## Boundaries

- A probe must not mutate world or character state unless its named purpose is
  to exercise that mutation in a bounded developer test.
- A visual probe must remove every object and runtime hook that it created.
- Probe failure must not disable gameplay or interrupt the behavior under test.
- Always-on events and enabled probes must not cause noticeable slowdown during
  normal play.
- A large snapshot must limit its output. It may write the full result to a
  local artifact instead of sending every record through normal diagnostics.
  It must still emit a typed event that identifies the run and records its
  result.
- Probe availability is a developer surface. It does not belong in the normal
  player menu unless the behavior becomes a supported player feature.

## In Development

The existing catalogs, comfort snapshot, and collider overlay prove parts of
this direction, but they do not yet form one system.

The first implementation slice must:

1. register snapshots and watchers in one catalog that native completion can
   discover;
2. continue recording important gameplay events at all times;
3. support session-only watcher settings and show the shipped default, session
   setting, and effective state;
4. add native console completion for the three command families;
5. prevent probe failures from interrupting gameplay and clean up on world
   exit, logout, and plugin reset; and
6. prove that the registry shipped with Benheim can discover and run one
   snapshot and one watcher.

The Wisp Echo render experiment is a useful first feature to use a watcher. It
should prove that a watcher can attach while the game runs, observe a named
behavior for a limited time, show when it is active, and clean up. The full Wisp
Echo product remains outside Developer Diagnostics.
