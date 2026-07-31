# Valheim Server and Mods

This repo treats the dedicated server and its optional mods as one product: a
durable shared Valheim world that removes repetitive chores without making
friends manage a fragile modpack.

The default compatibility promise is simple. Anyone with a vanilla PC or
console client should still be able to join. Client mods remain optional, and
server mods should create effects that vanilla clients can observe without
installing anything.

## Product Boundaries

- The world must remain recoverable through tested local and off-box backups.
- Server-side mods should preserve vanilla-client compatibility unless that
  tradeoff is changed deliberately.
- Client-side mods should not add custom persistent world objects or item data.
- Quality-of-life changes should remove repetition without erasing meaningful
  progression, resource costs, or multiplayer coordination.
- Mod infrastructure is part of server operations. Players should not need to
  understand BepInEx, Jotunn, systemd, or deployment mechanics to play.

## Mod Breakdown

| Mod | Product role | Runs on | Required for friends |
| --- | --- | --- | --- |
| BenheimQoL | First-party quality-of-life behavior such as inventory, repair, portal, and mining improvements. | Client | No |
| MassFarming | Third-party batch planting and harvesting. | Client | No |
| Eternal Fire | Third-party persistent fuel for fires, torches, hearths, braziers, and similar pieces. | Server | No |

BepInEx is the plugin loader used where mods run. Jotunn is a shared Valheim mod
library required by Eternal Fire. They are runtime dependencies, not separate
player-facing features.

BenheimQoL's detailed product behavior is owned by
[`mods/benheim-qol/PRODUCT.md`](mods/benheim-qol/PRODUCT.md). Third-party mod
behavior remains owned by each upstream project; this document records only why
the mod belongs in our stack and what compatibility promise it must preserve.

## Acceptance Shape

The server product is healthy when the world survives restarts and restores,
vanilla clients can join, backups remain usable, and enabled server mods produce
the same shared effect for modded and unmodded players.

An optional client mod is healthy when players without it remain compatible and
installing or removing it does not corrupt shared world or character data.

## Open Gates

- Prove the first server-side mod deployment with Eternal Fire, including a
  pre-deploy backup, vanilla-client join, visible persistent fire behavior, and
  a quick rollback path.
- Stabilize BenheimQoL's current behavior before expanding its feature set.
- Decide whether craft-from-nearby-containers belongs in BenheimQoL without
  removing the resource-budgeting information players use to make decisions.
