# Native Creature Stars

Valheim's star system is one persistent integer level on a `Character`.
Ordinary spawning chooses the level once. Native systems then derive health,
attack damage, loot, HUD stars, and optional prefab visuals from it. Native AI
does not use the level.

This gives Enemy Tiers a useful identity and signal, but not a behavioral
framework. Any creature-specific mechanics or AI variation would be new
Benheim behavior layered on a native level. This document records research,
not a product promise or implementation plan.

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

No native level branch changes movement speed, damage resistances, attack
choice, attack timing, attack range, push force, targeting, perception,
pathfinding, or AI decisions. `BaseAI`, `MonsterAI`, and `Humanoid` do not call
`Character.GetLevel()`. `Attack` uses the level only when it multiplies damage.

The player-visible result is simple: starred enemies are tougher, hit harder,
drop more opted-in loot, and carry native visual signals. They do not fight
differently because of their stars.

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
  Owner-side `MonsterAI.UpdateAI()` and `DoAttack()` are the more invasive
  seams for genuine decision or attack-selection changes.

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
| World modifiers | `Game.m_enemyLevelUpRate`, `Game.m_worldLevel`, `Game.UpdateWorldRates()` |
| Persistent level and health | `Character.m_level`, `Awake()`, `SetLevel()`, `SetupMaxHealth()`, `ZDOVars.s_level` |
| Offensive scaling | `Attack.ModifyDamage()`, `GetLevelDamageFactor()` |
| Loot scaling | `CharacterDrop.Drop.m_levelMultiplier`, `GenerateDropList()` |
| Presentation | `EnemyHud.UpdateHuds()`, `LevelEffects.SetupLevelVisualization()` |
| Absence of behavioral scaling | Search `Character.GetLevel()` references in `BaseAI`, `MonsterAI`, `Humanoid`, and `Attack` |

## What Remains Unknown

The next investigation needs one player-facing choice: select one creature and
state what a player should notice and do differently against its one-star
version. That answer determines whether to trace one authored attack seam or
one AI decision seam. A general alpha architecture is premature.

This research did not enumerate every creature's serialized spawn limits or
`LevelEffects` setup. It also did not prove prefab-specific collider changes,
choose new mechanics, or test a modified creature in multiplayer.

Valheim 1.0 can change any of these seams. After migration, revalidate the
assembly version and hash, the named level-selection and scaling methods, the
native ZDO fields, the owner-side AI gates, and Benheim's Leech patch boundary
before relying on this report.
