# Valheim Dev

Valheim Dev is Codex's live workbench for Valheim. Ben can stay in a disposable
test world while Codex inspects the running game, changes it, observes the
result, and tries another version. This removes the build, package, install, and
relaunch cycle from early development.

Valheim Dev is for making and understanding Benheim, not for playing it. Ben
continues to use Valheim and its native console. He does not need a second CLI,
dashboard, or editor.

## Ben And Codex Work In One Live Loop

The intended workflow is:

```text
Ben enables one disposable Lab world session.
target = Codex inspects what exists in the running game.
Codex describes the target's live structure and available behavior.
change = Codex applies a managed live change.
evidence = Codex watches the relevant signals and captures the visible result.
Ben judges how the change looks, feels, or plays.
Codex replaces the change and the loop repeats.
Codex removes the live change when testing ends.
chosen = Ben selects the behavior to keep.
shipping = The chosen behavior enters normal Benheim source and a normal build.
```

Inspection includes live objects, values, components, hierarchy, and available
methods. A target can come from the player, the object under the crosshair, the
hovered interface, or a bounded search of the current scene. This lets Codex
describe the object that actually exists instead of guessing only from source
or decompiled code.

Codex can apply managed live changes to code, runtime state, presentation, and
gameplay behavior. A managed live change remains active until Codex removes or
replaces it. If a replacement fails, Valheim Dev keeps the working version
active.

Some actions happen once and cannot honestly be removed. Spawning a creature,
dealing damage, or invoking a world-changing method may already have changed
the disposable world. Valheim Dev distinguishes those actions from managed
changes instead of promising false cleanup.

## What This Should Make Fast

- Inspect an unfamiliar live object, understand its useful structure, and
  connect that runtime evidence to the existing decompiler.
- Tune interface layout, materials, animation, particles, sound, or other
  presentation while Ben watches the same running game.
- Tune movement, combat, physics, status effects, and other mechanics while Ben
  plays, with only the relevant state observed.
- Point at a portal, plant, creature, collider, build piece, or item and inspect
  the exact instance involved in a bug.
- Leave a bounded watcher active while Ben reproduces a problem, then compare
  the observed transitions with existing Benheim diagnostics.
- Compare several variants quickly, remove the temporary work, and promote only
  the version Ben wants into shipped code.

The first live use is Affinity weapon-icon animation. Codex will inspect the
actual inventory and hotbar objects, apply visible variants, and replace them
while Ben judges the result. The chosen variant becomes shipped behavior only
after it is incorporated into Benheim and included in a normal build.

## Evidence Stays Attached To The Change

Valheim Dev keeps a persistent record of what Codex ran, which Valheim and
Benheim builds were active, what Codex targeted, what selected evidence it
observed, and whether cleanup succeeded. Active changes and watchers remain
visible so a later operation cannot silently forget what is still installed.

The record is evidence for the specific observations Codex selected. It does
not imply that Valheim Dev captured every downstream effect. Ben's observation
remains primary for look and feel.

[Benheim Developer Diagnostics](../../client-mods/benheim/src/DeveloperDiagnostics/PRODUCT.md)
continues to own typed events from shipped gameplay, Axiom delivery, and
in-game diagnostic controls. Valheim Dev can watch and correlate those events.
It does not create a second gameplay logging system.

## The Power Stays Inside A Disposable Lab

Ben alone creates, selects, resets, and deletes the disposable local test
character and world. Both remain outside the repository. After Ben enters that
world, he runs `bh lab on`. This authorizes repeated Codex operations for that
world session without separate approval for each operation.

Running `bh lab off`, leaving the world, or quitting Valheim ends the
authorization. Valheim Dev rejects new work, stops active watches, and attempts
to remove managed live changes. Authorization applies only to the current world
session. Valheim Dev cannot enable Lab mode, manage saves, launch Valheim, quit
Valheim, or restart Valheim.

Valheim Dev may connect only to the authorized local single-player Lab session.
It must never connect to an ordinary Benheim session, the shared production
world, or a dedicated server.

Valheim Dev may run trusted, bounded code with direct access to Unity and
Benheim. That code must return control to the game loop. Valheim Dev cannot
sandbox the code or forcibly stop it if it hangs Unity's main thread. Cleanup
is best effort. When cleanup is uncertain, Valheim Dev stops making changes and
tells Ben that Valheim must restart. Ben decides whether to restart Valheim or
recreate the disposable saves.
