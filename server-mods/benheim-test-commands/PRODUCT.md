# Benheim Test Commands

Benheim Test Commands is the dedicated-server half of selected, temporary
Benheim gameplay-test commands. It does not expose Valheim's developer command
surface.

## Current Behavior

The plugin exposes exactly three spawn requests to a connected native
administrator using Benheim `0.1.72`. `bh spawn boar 0` creates one native
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

Using the commands requires Benheim `0.1.72` on the requesting player and
Benheim Test Commands `0.1.2` on the dedicated server. Ben confirmed that
each of the three spawn requests created the requested native Boar tier exactly
once.

## In Development

The server component derives the starred-Boar physical profile while it owns a
spawned test Boar. Other connected players derive the same profile if they
later own that creature. Ownership migration and physical-profile coherence
remain unproven.

### Native henge overlay

`bh henge on` asks the dedicated server for every planned `StoneHenge1`,
`StoneHenge3`, `StoneHenge4`, and `StoneHenge5` location. The server accepts
the request only from a connected native Valheim administrator. It reads the
already-initialized native location plan. For every matching planned location,
it returns only the coordinates, whether or not Valheim marks that location as
placed.

The requesting client replaces its previous henge overlay with native
`Icon3` map pins. The pins have no labels, remain local to that client, and are
not saved. `bh henge off` removes only the pins owned by this overlay. Neither
command writes custom progress.

If the native location plan is not ready, the server rejects the request and
tells the requesting player. The request does not start location generation,
load a location prefab, or place a zone. It does not reveal the henge variant
or whether a planned henge would contain a Vegvisir. It does not reveal map
terrain, fog, or any Yagluth boss marker. The request does not write world
state, character state, or zone data object (ZDO) state.

This read-only overlay preserves the pre-1.0 Deep North boundary. It reads
Valheim's existing world plan without generating, loading, placing, or
exploring any zone.

The implementation adapts the location lookup and `Icon3` temporary-pin
pattern from the Unlicense-licensed
[`valheim-dev` `find` command](https://github.com/JereKuusela/valheim-dev/blob/359e59c3d2fd2c40a6e2bb1e447723d6180c89b1/ServerDevcommands/Commands/Find.cs),
without importing its general remote-command framework.

[`../../client-mods/benheim/src/EnemyTiers/PRODUCT.md`](../../client-mods/benheim/src/EnemyTiers/PRODUCT.md)
owns the Boar physical experiment and its gameplay acceptance boundary.
