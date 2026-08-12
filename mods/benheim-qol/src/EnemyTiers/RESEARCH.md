# Native Creature Stars

Valheim's star system is one persistent integer level on a `Character`.
Ordinary spawning chooses the level once. Native systems then derive health,
attack damage, loot, HUD stars, and optional prefab visuals from it. Native AI
does not use the level.

This gives Enemy Tiers a useful identity and signal, but not a behavioral
framework. Any creature-specific mechanics or AI variation would be new
Benheim behavior layered on a native level. This document records research,
not a product promise or implementation plan.

[Creature Mechanics research](../CreatureMechanics/RESEARCH.md) owns the
reusable creature AI, attack, damage, drop, animation, asset, and authority
control inventory. This document keeps only the evidence specific to star
identity, spawning, encounter context, Alpha feasibility, and retaliation.

## Evidence Baseline

These findings apply to Valheim `0.221.12`. The installed
`assembly_valheim.dll` had SHA-256
`ada4bd74e926680f7c17e1275b344ad2a9afd5759bf52146ad1a4c7866721b48`.
The assembly's `Version.CurrentVersion` also reports `0.221.12`.

The conclusions come from direct ILSpy decompilation of that exact assembly.
The source helpers in the root `PROMPT.md` own the current procedure for
resolving the installed assembly and inspecting the named types below. Verify
the assembly hash before comparing future output with this report.

## How Ordinary Spawns Choose Stars

The ordinary path is `SpawnSystem.UpdateSpawning()` to `UpdateSpawnList()` to
`Spawn()`. It runs only on the peer that owns the `SpawnSystem` network view.
Each serialized `SpawnSystem.SpawnData` supplies these level inputs:

- `m_minLevel` and `m_maxLevel`
- `m_levelUpMinCenterDistance`
- `m_overrideLevelupChance`

After the creature prefab is instantiated, `Spawn()` starts at the minimum
level. It makes one independent level-up roll for each step until it reaches
the maximum or the first roll fails. `GetLevelUpChance()` defaults to 10
percent per step. `Game.m_enemyLevelUpRate` and `Game.m_worldLevel` can change
that chance.

For a minimum level of 1 and maximum level of 3, normal world settings produce
this distribution when the distance gate passes:

| Stars | Character level | Probability |
| --- | ---: | ---: |
| 0 | 1 | 90% |
| 1 | 2 | 9% |
| 2 | 3 | 1% |

A matching nearby `SE_Stats` pheromone effect can override the starting level
and multiply the level-up chance. Its minimum-level override is not clamped to
`SpawnData.m_maxLevel`, so the serialized maximum caps the ordinary roll, not
every native override.

Biome, time, environment, progression keys, and population limits decide
whether the configured entry can spawn. They do not alter this level roll.
The roll has no world-age or player-count input.

## Encounter Context At Spawn Time

Valheim has three relevant level-selection seams, not one universal spawn
record. `SpawnSystem.Spawn()` handles wilderness and event lists.
`CreatureSpawner.Spawn()` handles fixed authored spawners. `SpawnArea.SpawnOne()`
handles nests and other local spawn areas. Each seam runs on the network owner
of its source object.

The following context is available at those seams in `0.221.12`:

| Context | Direct evidence | Important boundary |
| --- | --- | --- |
| Actual biome | `ZoneSystem.GetGroundData()` or `WorldGenerator.GetBiome()` at the spawn point | `SpawnData.m_biome` is an allowed mask, not the sampled biome. |
| World-center distance | `Utils.LengthXZ(spawnPoint)`; native code also uses `spawnPoint.magnitude` for its level gate | This is stable and needs no saved state. |
| Day or night | `EnvMan.IsDay()` and `IsNight()` | The source entry's day and night flags identify authored night-only encounters more reliably than time alone. |
| Environment or weather | `EnvMan.GetCurrentEnvironment().m_name`; `SpawnData.m_requiredEnvironments` | This is the current peer environment, not a position-based world query. Weather is transient, so it is a poor area-danger input. |
| Authored location | `Location.GetLocation(spawnPoint)` for active locations; `ZoneSystem.m_locationInstances` and `GetLocationList()` for prefab identity, position, and radii | `Location` is cheap and exact for loaded authored locations. |
| Dungeon | `Character.InInterior(spawnPoint)` and `Location.GetLocation()` | `InInterior()` is only the native height test. The location supplies the authored dungeon identity. |
| Village or camp | A matching `ZoneSystem.LocationInstance` or active `Location` | There is no native village or camp category. The prefab identity gives the meaning. |
| Arbitrary structure | No common semantic registry | Some structures are locations, some are local spawner prefabs, and some are ordinary world or player pieces. A generic structure-proximity rule is not cheap or reliable. |
| Local spawner identity | The current `CreatureSpawner` or `SpawnArea`, its selected creature prefab, and its transform | This identity is available only inside that source method. The spawned creature does not keep a native source tag. |
| Global progression | `ZoneSystem.GetGlobalKey()` and `GetGlobalKeys()` | Keys can gate encounter entries without changing old-area levels. |
| Spawn source | `SpawnSystem.Spawn()` receives `SpawnData` plus `eventSpawner`; the other two source classes have separate methods | Native creatures persist `eventCreature` and `despawnInDay`, but not a general source identity. |

This boundary matters for implementation. A patch at `Character.SetLevel()` is
too late to recover every source. A future context rule must run at the three
source seams or attach its result before the source method returns.

## How Valheim Authors Danger

Valheim already separates wilderness pressure from authored territory:

- `SpawnSystem.SpawnData` filters wilderness and event entries by biome,
  distance, time, environment, global keys, terrain, group size, and population.
- `ZoneSystem.ZoneLocation` places authored locations by biome, world-center
  distance, quantity, spacing, terrain, and forest rules.
- `Location` defines exterior and interior radii. It can override minimum
  level, maximum level, and level-up chance for its `CreatureSpawner` children.
- `CreatureSpawner` defines one fixed source with its own creature, levels,
  day or night flags, global-key gates, trigger, respawn, and spawn group. Its
  time gate controls creation only; this path does not set `despawnInDay`.
- `SpawnArea` defines a persistent local threat such as a nest. It chooses from
  weighted creatures and enforces near and total population limits. It has no
  native day or night field.
- Dungeons are authored locations whose generated rooms contain their normal
  prefab content, including local spawners. Their interior is placed above
  world height and maps back to the exterior location's zone.
- A night-only `SpawnData` entry sets `MonsterAI.despawnInDay` on its creature.
  During day it stops hunting and moves away to despawn once it has no visible
  target. It does not necessarily vanish at dawn during an active fight.
- `RandomEvent` combines a visible start message, duration, range, biome and
  progression gates, optional environment and music, and an event spawn list.
  The event itself has no day or night gate, but each event `SpawnData` entry
  keeps the ordinary time flags. `RandEventSystem` chooses the event on the
  server and synchronizes its name, time, and position to every peer.

The installed asset manifests provide representative authored examples without
requiring a full catalog: `GoblinCamp2`, `TrollCave02`, and `SunkenCrypt4` are
location prefabs; `DG_GoblinCamp` and `DG_SunkenCrypt` are dungeon prefabs;
`Spawner_GreydwarfNest`, `BonePileSpawner`, and
`Spawner_Skeleton_night_noarcher` are local spawner prefabs. These names prove
the installed assets, not unextracted serialized field values.

## Candidate: One Encounter Context Rule

One small rule can support the current product direction without custom world
data:

1. Classify an authored night-only entry as **Night Special** when its source
   allows night and disallows day.
2. Otherwise classify an eligible enemy from `CreatureSpawner` or `SpawnArea`,
   or any dungeon result, as **Enemy Territory**.
3. Otherwise classify wilderness by sampled biome and a biome-specific
   world-center distance band: **Far Wilderness** or **Ordinary Wilderness**.
4. Roll native stars within that encounter context. Use global keys later to
   unlock new encounter entries, not to inflate old areas.

This candidate intentionally drops generic structure proximity. Local spawner
identity already covers nests, camps, and dungeon enemies with less ambiguity.
Authored `Location` identity remains available for a small explicit exception
when playtesting proves one is valuable.

This is a feasibility candidate, not a chosen formula. The distance bands,
level distributions, and eligible sources remain product decisions.

## What Players Get From Stars

Levels are one-based. The enemy HUD maps levels 1, 2, and 3 to zero, one, and
two stars.

| Effect | Level 1 | Level 2 | Level 3 | Evidence |
| --- | ---: | ---: | ---: | --- |
| Maximum health | 1x | 2x | 3x | `Character.SetupMaxHealth()` |
| Attack damage | 1x | 1.5x | 2x | `Attack.GetLevelDamageFactor()` |
| Level-aware drop factor | 1x | 2x | 4x | `CharacterDrop.GenerateDropList()` |

The drop factor applies only when a serialized drop enables
`m_levelMultiplier`. It multiplies both the drop chance and the amount. Chance
effectively caps at 100 percent, and the final amount caps at 100.

Stars also increase the absolute damage required to stagger a creature.
`Character.GetStaggerTreshold()` derives the threshold from maximum health and
the prefab's `m_staggerDamageFactor`.

`EnemyHud.UpdateHuds()` activates the level-2 or level-3 star object while
keeping the normal hover name. `LevelEffects` can apply prefab-authored scale,
material color, emission, or enabled objects. It scales its own transform, not
an intrinsic creature-level collider or navigation value. Universal hitbox or
pathing changes are therefore not proven.

No native level branch changes creature behavior. `BaseAI`, `MonsterAI`, and
`Humanoid` do not call `Character.GetLevel()`. `Attack` uses the level only
when it multiplies damage. The shared creature-mechanics reference owns the
full list of controls that remain prefab-authored or would require new
Benheim behavior.

The player-visible result is simple: starred enemies are tougher, hit harder,
drop more opted-in loot, and carry native visual signals. They do not fight
differently because of their stars.

## Candidate: Species Retaliation Through Native Events

The native death and event seams can support a transient per-species kill
window, but the aggregation must be server-authoritative.

`Character.ApplyDamage()` stores the last applied `HitData` in
`m_lastHit`. `HitData` serializes its attacker as a `ZDOID`, and
`GetAttacker()` resolves that networked character. `Character.OnDeath()`
already uses this value for native last-hit statistics. A retaliation hook can
therefore accept only an untamed enemy whose last attacker resolves to a
`Player` and ignore environmental or unresolved deaths.

This is direct last-hit credit, not complete player-caused kill credit.
`SE_Poison` and `SE_Burning` apply attacker-less tick damage, which overwrites
`m_lastHit`. A player-caused damage-over-time death is therefore unattributed.
The smallest prototype can count direct final hits only. Full credit would need
a custom recent-attacker window on each victim.

The creature's network owner runs the authoritative death. That owner is not
necessarily the server. The minimum multiplayer boundary is:

1. Run the death hook once on the victim's current network owner.
2. Report the victim species, victim identity, responsible player character
   ID, and kill position through a routed RPC to the server.
3. Let the server deduplicate reports and own the per-species timestamps,
   warning state, active response, and cooldown.
4. When the threshold passes, match the responsible player's character ID to a
   `ZNetPeer`, use its current `m_refPos`, and call
   `RandEventSystem.SetRandomEventByName()` on the server.

`ZRoutedRpc` supplies the reporting peer ID, but the kill payload is still a
client report. Cooperative multiplayer only needs deduplication and current
player checks. Hostile-client validation would need a separate design and is
not proven here.

`RandEventSystem` already supplies the visible and synchronized response. The
server's `SetRandomEvent()` clone sends `SetEvent` with the event name, time,
and position to every peer. Players inside the event range activate it and see
its start message. Their zone-owned `SpawnSystem` instances consume the active
event's `SpawnData`. Native code marks those creatures with the synchronized
`eventCreature` ZDO flag. After the event they stop hunting and move away to
despawn once they have no target and are no longer alerted.

The prototype must not replace an unrelated active native event. It can skip a
trigger while `RandEventSystem.HaveActiveEvent()` is true. A custom retaliation
event definition must exist on the server and every client before the server
sends its name. Reusing a suitable native event avoids that registration work,
but it does not guarantee the desired species or encounter shape.

`RandEventSystem` has one global `m_randomEvent` slot. Starting another event
stops and replaces the current one. The native seam therefore supports only one
retaliation or raid at a time, not concurrent per-player or per-species
responses. Independent simultaneous retaliations would require a different
spawn and synchronization seam and are outside this candidate.

Valheim has no native party or group identity in these seams. The smallest
prototype keys heat to the responsible player. Nearby companions still share
the warning and fight because event activation is spatial. Pooling kills across
a group would require a transient proximity-cluster rule, not persistent world
data.

The kill window, warning, and cooldown can remain in server memory and reset on
restart. No custom ZDO or world-save field is required. Once a native event
starts, `RandEventSystem.PrepareSave()`, `SaveAsync()`, and `Load()` already
preserve its name, time, and position. Persisting heat or cooldown across a
restart would be a separate product choice.

This candidate requires compatible Benheim code on the server and every peer
that can own an active zone. A custom event also requires every client to know
its definition. The current evidence does not support a client-only shared
world implementation.

## Authority And Persistence

The zone owner chooses the level because ordinary spawning requires
`SpawnSystem.m_nview.IsOwner()`. `Character.SetLevel()` writes the native ZDO
field `ZDOVars.s_level` and recalculates ZDO-backed maximum health.
`Character.Awake()` restores the level for non-player characters.

A mechanic that is a deterministic function of creature prefab and native
level can reuse this synchronized identity. Authoritative behavior must still
run on the creature's current network owner. Every peer that can own an active
zone must run compatible Benheim behavior.

A future variant that cannot be derived from prefab and level would need an
explicit persistence and ownership design. This report does not choose one.

## Clean Extension Seams

- `SpawnSystem.Spawn()` is the narrow point where ordinary spawning finishes
  the native level choice and calls `Character.SetLevel()`.
- `Character.SetLevel()` and `m_onLevelSet` expose the level lifecycle for
  deterministic setup. `LevelEffects` already consumes this lifecycle.
- `LevelEffects` and `EnemyHud` are the native presentation seams.
- `Attack.ModifyDamage()` is the existing offensive level seam.
- The shared creature-mechanics reference owns the owner-side AI,
  attack-selection, attack-execution, and target-owner damage seams.

The current Leech change does not collide with native star selection.
`LeechSpawnPatches` postfixes `SpawnSystem.Awake()` and changes only the
ordinary Leech `SpawnData.m_spawnInterval`. It does not change level fields or
patch spawn execution. Future work that rebuilds `m_spawnLists`, also patches
`SpawnSystem.Awake()`, or changes the Leech interval must preserve this
adjustment deliberately. The existing patch declares no Harmony ordering.

## Reproduction Map

| Conclusion | Direct evidence in `0.221.12` |
| --- | --- |
| Version baseline | `Version.CurrentVersion` |
| Ordinary level selection | `SpawnSystem.SpawnData`, `UpdateSpawning()`, `UpdateSpawnList()`, `Spawn()`, `GetLevelUpChance()` |
| Fixed and area level selection | `CreatureSpawner.UpdateSpawner()`, `Spawn()`; `SpawnArea.UpdateSpawn()`, `SpawnOne()` |
| Biome, distance, time, environment, keys | `ZoneSystem.GetGroundData()`, `WorldGenerator.GetBiome()`, `Utils.LengthXZ()`, `EnvMan`, `ZoneSystem.GetGlobalKey()` |
| Authored location and dungeon context | `ZoneSystem.ZoneLocation`, `LocationInstance`, `GetLocationList()`; `Location.GetLocation()`, `Character.InInterior()` |
| Location level overrides | `Location.m_enemyMinLevelOverride`, `m_enemyMaxLevelOverride`, `m_enemyLevelUpOverride`; `CreatureSpawner.Spawn()` |
| Night-only lifecycle | `SpawnData.m_spawnAtDay`, `CreatureSpawner.m_spawnAtDay`, `MonsterAI.SetDespawnInDay()`, `ZDOVars.s_despawnInDay` |
| Native event selection and display | `RandomEvent`; `RandEventSystem.SetRandomEventByName()`, `SetRandomEvent()`, `RPC_SetEvent()`, `SetActiveEvent()` |
| Native event spawns and cleanup | `RandEventSystem.GetCurrentSpawners()`; `SpawnSystem.UpdateSpawning()`, `Spawn()`; `MonsterAI.SetEventCreature()`, `UpdateAI()` |
| Native event save boundary | `RandEventSystem.PrepareSave()`, `SaveAsync()`, `Load()` |
| Last-hit attribution and limit | `Character.ApplyDamage()`, `m_lastHit`, `OnDeath()`; `HitData.m_attacker`, `GetAttacker()`; `SE_Poison.UpdateStatusEffect()`, `SE_Burning.UpdateStatusEffect()` |
| Client-to-server seam and player position | `ZRoutedRpc.InvokeRoutedRPC()`, `Register()`; `ZNet.IsServer()`; `ZNetPeer.m_characterID`, `m_refPos` |
| World modifiers | `Game.m_enemyLevelUpRate`, `Game.m_worldLevel`, `Game.UpdateWorldRates()` |
| Persistent level and health | `Character.m_level`, `Awake()`, `SetLevel()`, `SetupMaxHealth()`, `ZDOVars.s_level` |
| Offensive scaling | `Attack.ModifyDamage()`, `GetLevelDamageFactor()` |
| Loot scaling | `CharacterDrop.Drop.m_levelMultiplier`, `GenerateDropList()` |
| Presentation | `EnemyHud.UpdateHuds()`, `LevelEffects.SetupLevelVisualization()` |
| Absence of behavioral scaling | Search `Character.GetLevel()` references in `BaseAI`, `MonsterAI`, `Humanoid`, and `Attack` |

## What Remains Unknown

Research is sufficient to choose an encounter-context prototype and a
server-authoritative retaliation prototype. Product work still needs to choose
distance bands, level distributions, one species, one response, and whether
nearby players pool their kills. Those are experience decisions, not missing
Valheim seams.

This research did not extract every prefab's serialized spawn settings, define
a generic structure taxonomy, prove hostile-client validation, choose new tier
mechanics, or test modified multiplayer behavior. A general Alpha, ecology, or
raid architecture remains premature.

Valheim 1.0 can change any of these seams. After migration, revalidate the
assembly version and hash, all three level-selection seams, location and event
types, death attribution, routed RPC behavior, native ZDO fields, owner-side AI
gates, and Benheim's Leech patch boundary before relying on this report.
