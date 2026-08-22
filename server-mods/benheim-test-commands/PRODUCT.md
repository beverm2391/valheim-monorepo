# Benheim Test Commands

Benheim Test Commands is the dedicated-server half of selected, temporary
Benheim gameplay-test commands. It does not expose Valheim's developer command
surface.

## Current Behavior

The plugin exposes exactly three spawn requests to a connected native
administrator using Benheim `0.1.67`. `bh spawn boar 0` creates one native
unstarred Boar as a control. `bh spawn boar 1` creates one native one-star
Boar. `bh spawn boar 2` creates one native two-star Boar. `bh help` lists
`bh spawn boar 0|1|2` and explains that `0` is the unstarred control, `1` is one
star, and `2` is two stars. The dedicated server validates the requesting peer
against Valheim's existing admin list before spawning a Boar nearby. It uses
that peer's current server-known position, instantiates the fixed native Boar
prefab, sets native level `1`, `2`, or `3`, and returns an explicit accepted or
rejected result.

The command does not accept prefab names or provide item, terrain, teleport,
world-key, kill, or general command execution. It does not enable remote
developer commands or create another permission system. The spawned Boar keeps
its native prefab identity and native saved level.

Using the commands requires Benheim `0.1.67` on the requesting player and
Benheim Test Commands `0.1.1` on the dedicated server. Server component `0.1.1`
is deployed and its exact plugin load is runtime-confirmed. Ben confirmed that
each of the three spawn requests created the requested native Boar tier exactly
once.

## In Development

The server component derives the starred-Boar physical profile while it owns a
spawned test Boar. Other connected players derive the same profile if they
later own that creature. Ownership migration and physical-profile coherence
remain unproven.

### One-at-a-time Yagluth henge search

`bh henge mark` is a proposed aid for native administrators who want to find
Yagluth without revealing the boss altar or every possible search location.
The server selects only `StoneHenge1`, `StoneHenge3`, `StoneHenge4`, and
`StoneHenge5`. When Valheim places one of these locations, that location has
Valheim's normal 40 percent chance of containing a Yagluth Vegvisir.

The server ignores henges that Valheim already marks as placed. It sorts the
remaining candidates by horizontal distance from world center and returns only
the nearest candidate. The client removes its previous Benheim henge marker
and adds one temporary native map pin named `Henge candidate`. Calling the
`bh henge mark` command again before Valheim places that henge returns the same
candidate. After the player approaches that henge and Valheim places it,
calling `bh henge mark` returns the next eligible candidate.

`bh henge clear` removes the temporary Benheim henge pin. Only the requesting
administrator sees the pin, and it is not saved. After that administrator
reconnects, `bh henge mark` recovers the nearest candidate that Valheim has
not marked as placed from the current server state.

The command does not reveal whether the candidate contains a Vegvisir. It does
not reveal the selected henge variant or map terrain. It does not place a
Yagluth boss marker, teleport a player, change location generation, or write
custom progress to the world or character. A zone can become placed when a
player travels near it without inspecting the henge. Valheim's native placement
behavior can then cause that candidate to be skipped later. The first version
accepts this tradeoff instead of storing discovery state for each player.

The implementation should adapt the server-side location lookup and temporary
pin pattern from the Unlicense-licensed
[`valheim-dev` `find` command](https://github.com/JereKuusela/valheim-dev/blob/359e59c3d2fd2c40a6e2bb1e447723d6180c89b1/ServerDevcommands/Commands/Find.cs),
without importing its general remote-command framework.

[`../../mods/benheim-qol/src/EnemyTiers/PRODUCT.md`](../../mods/benheim-qol/src/EnemyTiers/PRODUCT.md)
owns the Boar physical experiment and its gameplay acceptance boundary.
