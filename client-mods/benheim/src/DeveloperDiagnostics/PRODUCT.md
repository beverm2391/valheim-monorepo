# Developer Diagnostics

Developer Diagnostics lets Ben play normally while Benheim records structured
evidence that a developer can query afterward. It uses Benheim's existing typed
diagnostics, local NDJSON, and Axiom delivery. It does not add a second logger,
schema, database, or remote destination.

The normal workflow is low-friction: core events and inexpensive event probes
already provide bounded evidence. A developer enables probes that ship off
only when this evidence cannot answer the question.

## Evidence Model

Developer Diagnostics distinguishes four kinds of evidence.

### Core events

Feature modules own typed events for important actions, decisions, state
changes, results, and cleanup. Core events remain enabled whenever their
feature runs. They do not record every frame or repeat unchanged state. The
root [Benheim product](../../PRODUCT.md) owns their local and remote delivery,
schema, identity, and privacy boundaries.

### Event probes

An event probe is a named typed diagnostic stream that adds bounded evidence to
core events. Event probes share one registry. Code registers each probe's name
and shipped default. During a session, the registry owns its `on`, `off`, or
`default` override and reports its effective state.

Low-cost, bounded event probes should normally ship on. Expensive, verbose, or
high-volume event probes normally ship off. Relaunching Benheim clears session
overrides and restores shipped defaults. Probe state is not saved to the
character or world.

### Snapshots

A snapshot inspects one bounded piece of current runtime state, emits a limited
result, and cleans up immediately. Runtime UI catalogs, effect catalogs, and
comfort inspection are snapshots. A large snapshot may write detailed records
to a bounded local artifact, but it still emits typed run and result evidence
through normal diagnostics.

### Visual probes

A visual probe temporarily renders information in the game, such as collider
overlays or a short rendering experiment. Visual probes normally ship off.
They remove every object and runtime hook they create when disabled, when the
world exits, when the player logs out, and when the plugin resets.

## Registry And Commands

One registry owns discovery, command dispatch, cleanup, and failure containment
for event probes, snapshots, and visual probes. For event and visual probes, it
also owns shipped defaults, session overrides, and effective state. Snapshots
run once and expose no persistent on/off state. Feature modules may register a
probe or emit through a registered event probe; they do not create competing
command or state systems.

Developer Diagnostics uses short commands because Valheim's native console can
complete command names and their first argument:

- `bhcatalog <effects|text|ui> [filter]` lists a bounded runtime catalog.
- `bhrun <snapshot>` runs one snapshot.
- `bhwatch` lists registered event and visual probes with their kind, shipped
  default, session override, and effective state.
- `bhwatch <probe> [on|off|default]` reports or changes one probe for the
  current session.

Console output is a compact operator surface, not the evidence store. Detailed
evidence remains typed and queryable in local NDJSON and Axiom.

## Spawn Population Probe

The first event probe is `spawns`. It observes only native spawn rules that a
feature explicitly registers; it does not log every creature or scan the whole
world. It ships on because its work is bounded and answers balance questions
that successful-spawn events alone cannot answer.

For each registered rule, the probe records the effective rule configuration
when the rule becomes available and whenever it changes. This includes the
source, prefab, effective spawn interval and chance, loaded-population cap,
group size, spacing, biome, and altitude constraints.

While the rule is active, the probe records bounded population state. It emits
when the loaded count changes, when the population enters or leaves its cap,
and at a low-frequency heartbeat so an unchanged saturated or sparse state
remains observable. Population evidence includes the current loaded count,
cap, and saturation state. Existing feature-owned success events remain core
events.

The initial probe does not instrument every rejection branch inside Valheim's
native spawn loop. If configuration, population, cap transitions, and success
events cannot explain a result, a later verbose event probe may add narrowly
scoped rejection evidence and ship off by default.

## Boundaries

- A probe must answer one specific question and bound its duration, output,
  cardinality, and performance cost.
- Enabled probes must not record every frame. They may repeat unchanged state
  only through a bounded, low-frequency heartbeat.
- Probe failure must not disable gameplay or interrupt the behavior being
  observed.
- A probe must not mutate world or character state unless its named purpose is
  to exercise that mutation in a bounded developer test.
- New native or Harmony observation seams still require a new Benheim build.
  Runtime controls attach or detach only code already present in that build.
- Probe controls are developer surfaces and do not belong in the normal player
  menu unless the behavior becomes a supported player feature.

## Current Candidate

The installed `0.1.80` candidate contains the first registry slice: runtime
catalogs, the comfort snapshot, and the `colliders` visual probe. It does not
yet contain the generic event-probe registry or the `spawns` probe described
above.

Live `0.1.80` testing accepted the comfort snapshot's mechanics. Calculated
comfort and cached comfort were both `9`, and typed diagnostics contained
complete per-piece evidence. Its console presentation did not pass because
Valheim's non-scrollable console flooded and hid the useful result. The
approved correction keeps one short summary visible while complete per-piece
evidence remains typed and queryable.

The `colliders` visual probe is off by default. Live `0.1.80` testing accepted
the overlay's appearance. Live review still must prove command visibility,
snapshot cleanup, probe status, `off` and `default` transitions, and overlay
cleanup on world exit and logout.

The [Wisp Echo product](../WispEcho/PRODUCT.md) owns its render experiment and
acceptance result. Developer Diagnostics owns only the shared probe lifecycle,
status, bounded runtime work, and cleanup used to run that experiment.
