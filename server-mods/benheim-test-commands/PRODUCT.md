# Benheim Test Commands

Benheim Test Commands is the dedicated-server half of selected, temporary
Benheim gameplay-test commands. It does not expose Valheim's developer command
surface.

## In Development

The first candidate exposes exactly three spawn requests to a connected native
administrator using Benheim `0.1.61`. `bh spawn boar 0` creates one native
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

Using this candidate requires Benheim `0.1.61` on the requesting player and
Benheim Test Commands `0.1.0` on the dedicated server. The server component
also derives the same physical profile while it owns the spawned Boar. Other
connected players need compatible Benheim behavior to derive that profile if
they later own the creature. The server component is not deployed, and the
command behavior remains runtime-unproven.

[`../../mods/benheim-qol/src/EnemyTiers/PRODUCT.md`](../../mods/benheim-qol/src/EnemyTiers/PRODUCT.md)
owns the Boar physical experiment and its gameplay acceptance boundary.
