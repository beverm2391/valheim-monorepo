# Valheim Server and Mods

This repo treats the dedicated server and its optional mods as one product: a
durable shared Valheim world that removes repetitive chores without making
friends manage a fragile modpack.

The default compatibility promise is simple. Anyone with a vanilla PC or
console client can join and play normally. Client mods are required only for
features that explicitly name that requirement.

## Product Boundaries

- The world must remain recoverable through tested local and off-box backups.
- Server-side mods should preserve vanilla-client compatibility unless that
  tradeoff is changed deliberately.
- Client-side mods should not add custom persistent world objects or item data.
- Quality-of-life changes should remove repetition without erasing meaningful
  progression, resource costs, or multiplayer coordination.
- Mod infrastructure is part of server operations. Players should not need to
  understand BepInEx, systemd, or deployment mechanics to play.

## Gameplay Breakdown

| Feature | Product role | Runs on | Required for friends |
| --- | --- | --- | --- |
| Benheim | Quality-of-life features we maintain for inventory, farming, repair, portals, and mining. | Client | No |
| Benheim Eternal Fire | Automatically refuels supported native fires and lights; normal Valheim burn conditions still apply. | Server | No |
| Metal portals | Native world rule allowing normally restricted items through portals. | Server | No |
| Skill progression | Optional settings increase skill gain and reduce skill loss on death for every player. | Server | No |

BepInEx loads the mods. Benheim Eternal Fire does not depend on a shared mod
library.

Benheim's detailed product behavior is owned by
[`mods/benheim-qol/PRODUCT.md`](mods/benheim-qol/PRODUCT.md). Benheim Eternal
Fire's behavior and player experience are owned by
[`server-mods/benheim-eternal-fire/PRODUCT.md`](server-mods/benheim-eternal-fire/PRODUCT.md).
Third-party mod behavior remains owned by each upstream project; this document
records only why the mod belongs in our stack and what compatibility promise it
must preserve.

## Acceptance Shape

The server product is healthy when the world survives restarts and restores,
vanilla clients can join, backups remain usable, and enabled server-only gameplay
mods produce the same shared effect for modded and unmodded players.

An optional client mod is healthy when players without it remain compatible and
installing or removing it does not corrupt shared world or character data.
Put Away must use Valheim's native chest ownership flow so every connected
player sees the same completed chest state. It must not require a server plugin.
A player without Benheim must still be able to join and use chests normally.

## Open Gates

- Complete the temporary [Valheim 1.0 migration](MIGRATION-1.0.md): prove the
  existing world on vanilla 1.0, then restore or defer each mod deliberately.
- Complete Benheim Eternal Fire's remaining
  [gameplay and restart proof](server-mods/benheim-eternal-fire/PRODUCT.md).
- Decide whether faster sailing justifies client installs on both Mac and
  Windows. Every candidate examined so far requires a client install, so no
  server-only ship mod was deployed.
- Test the packaged Windows installer and desktop shortcut on a friend's PC.
  Friends have already tested the shareable Mac installer during gameplay.
- Prove the native Put Away flow in a focused two-player gameplay test before
  releasing that implementation.
- Stabilize Benheim's current behavior before expanding its feature set.
- Decide whether Benheim should support crafting from nearby containers without
  hiding the resource totals players use to plan.
