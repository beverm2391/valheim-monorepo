# Benheim Test Commands

Benheim Test Commands is the dedicated-server half of selected, temporary
Benheim gameplay-test commands. It does not expose Valheim's developer command
surface.

## Current Behavior

The plugin exposes exactly three spawn requests to a connected native
administrator using Benheim `0.1.66`. `bh spawn boar 0` creates one native
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

Using the commands requires Benheim `0.1.66` on the requesting player and
Benheim Test Commands `0.1.1` on the dedicated server. Server component `0.1.1`
is deployed and its exact plugin load is runtime-confirmed. Ben confirmed that
each of the three spawn requests created the requested native Boar tier exactly
once.

## In Development

The server component derives the starred-Boar physical profile while it owns a
spawned test Boar. Other connected players derive the same profile if they
later own that creature. Ownership migration and physical-profile coherence
remain unproven.

[`../../mods/benheim-qol/src/EnemyTiers/PRODUCT.md`](../../mods/benheim-qol/src/EnemyTiers/PRODUCT.md)
owns the Boar physical experiment and its gameplay acceptance boundary.
