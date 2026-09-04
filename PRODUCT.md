# Valheim Server and Mods

This repo treats the dedicated server and its mods as one product: a durable
shared Valheim world with curated changes that make the game more fun for our
group without turning it into a total overhaul or a fragile modpack.

Our regular group runs compatible Benheim versions. Versions need not be
identical if they preserve the behavior and shared data of every Benheim
feature the group uses. The product does not promise that an unmodded PC or
console client can join a Benheim session. Keep the vanilla launch path for
recovery and the Valheim 1.0 migration, not as a multiplayer compatibility
promise.

## Product Boundaries

- The world must remain recoverable through tested local and off-box backups.
- Operator secrets must stay out of local configuration. Deployment may create
  restricted runtime files on the server, but those files are never sources of
  truth.
- Make required client and server components explicit for each shared feature.
- Defer custom persistent world objects until a specific feature needs them.
  Approve their world, recovery, migration, and removal behavior as part of
  that feature design. Add custom item data only when the product design needs
  it and removal cannot corrupt a character.
- Benheim may combine quality-of-life features, balance changes, gameplay
  adjustments, and selected new mechanics. Quality of life is part of the mod,
  not its whole identity.
- Changes should preserve meaningful progression, resource costs, and
  multiplayer coordination unless changing one of them makes our game better.
- Prefer mechanics that extend Valheim's world, actions, progression, and
  visual language over parallel systems that feel pasted onto the game.
- Mod infrastructure is part of server operations. Players should not need to
  understand BepInEx, systemd, or deployment mechanics to play.

## Gameplay Breakdown

| Feature | Product role | Runs on | Required for friends |
| --- | --- | --- | --- |
| Benheim | Curated quality-of-life, balance, and gameplay changes for our group. | Client | Yes for our regular group. Each member must use a version compatible with those used by every other member. |
| Benheim Eternal Fire | Automatically refuels supported native fires and lights; normal Valheim burn conditions still apply. | Server | No |
| Benheim Test Commands | Runs a fixed native-admin command allowlist for selected Benheim gameplay experiments. | Client command and server component | Only the requesting native admin needs the matching client command. The server component is required. Every peer that can own the spawned test creature still needs compatible Benheim gameplay behavior. |
| Benheim Server Support | Coordinates Put Away and keeps each player's confirmed-kill chain on the server. | Server | Benheim `0.1.76` through `0.1.81` use Benheim Server Support `0.1.6` for Put Away and BERSERKER/SLAUGHTERHOUSE. Put Away uses lease generation `v2` and transaction generation `v4`. Kill Attribution V3 uses client-requested capability responses. |
| Metal portals | Native world rule allowing normally restricted items through portals. | Server | No |
| Skill progression | Optional settings increase skill gain and reduce skill loss on death for every player. | Server | No |

The deployed server plugin stack consists of one shared BepInEx installation,
Benheim Eternal Fire, Benheim Test Commands, and Benheim Server Support. None
requires another shared mod library.

Benheim's detailed product behavior is owned by
[`client-mods/benheim/PRODUCT.md`](client-mods/benheim/PRODUCT.md). Benheim Eternal
Fire's behavior and player experience are owned by
[`server-mods/benheim-eternal-fire/PRODUCT.md`](server-mods/benheim-eternal-fire/PRODUCT.md).
Benheim Test Commands' allowlist, native-admin boundary, and component
requirements are owned by
[`server-mods/benheim-test-commands/PRODUCT.md`](server-mods/benheim-test-commands/PRODUCT.md).
Benheim Server Support's production coordination boundary is owned by
[`server-mods/benheim-server-support/PRODUCT.md`](server-mods/benheim-server-support/PRODUCT.md).
Third-party mod behavior remains owned by each upstream project; this document
records only why the mod belongs in our stack and what compatibility promise it
must preserve.

## Product Candidates

[Crow](tools/crow-lab/PRODUCT.md) is a private companion candidate for Ben,
Johnny, and Ozi. Its current implementation is only a local writer's-room lab.
It is not part of Benheim, the deployed server stack, or the live acceptance
queue.

[Valheim Dev](tools/valheim-dev/PRODUCT.md) is an agent-only stdio MCP server
for an explicit local, single-player Lab session. Codex can apply self-contained
C# experiments, read their persistent results and selected evidence, and
compare variants without creating a new Benheim package, installing the
package, or relaunching Valheim between variants. It is not a player feature or
part of the deployed server stack.

## Acceptance Shape

The server product is healthy when the world survives restarts and restores,
backups remain usable, and the required client and server components work
together to produce the same shared behavior for the regular group.

Benheim is healthy when every member of our regular group has a version
compatible with those used by every other member. The shared world and
characters must remain recoverable across updates or removal.
Put Away must route each deposit to Valheim's current chest owner so every
connected player sees the same completed chest state. Benheim Server Support
must prevent compatible clients from entering that flow concurrently.

## Open Gates

- Complete the temporary [Valheim 1.0 migration](MIGRATION-1.0.md): prove the
  existing world on vanilla 1.0, then restore or defer each mod deliberately.
- Complete Benheim Eternal Fire's remaining
  [gameplay and restart proof](server-mods/benheim-eternal-fire/PRODUCT.md).
- Choose Benheim's next gameplay system by balancing the ideal player experience,
  continuity with Valheim, and the cleanest proven implementation path.
