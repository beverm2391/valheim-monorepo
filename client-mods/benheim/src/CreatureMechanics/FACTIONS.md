# Creature Factions

Factions tell ordinary AI and attacks who is hostile. They do not make a
character immune to damage. The evidence baseline and shared authority rules
live in [Creature Mechanics](RESEARCH.md).

## Native Factions

Valheim `0.221.12` defines these `Character.Faction` values:

- `Players`
- `AnimalsVeg`
- `ForestMonsters`
- `Undead`
- `Demon`
- `MountainMonsters`
- `SeaMonsters`
- `PlainsMonsters`
- `Boss`
- `MistlandsMonsters`
- `Dverger`
- `PlayerSpawned`
- `TrainingDummy`

These names are code identifiers, not strict biome boundaries. A creature's
prefab owns its `m_faction` value.

## Installed Prefab Examples

The examples below come from each root prefab's serialized
`Character.m_faction` value in the installed core SoftRef bundle `c4210710`.
That bundle had SHA-256
`2d1e17fa941213747868face6b8fb13e23332292454007255c42562119e31448`.
The list is representative, not a complete catalog of every variant.

| Faction | Verified examples from installed prefabs |
| --- | --- |
| `Players` | Player, `Skeleton_Friendly` |
| `AnimalsVeg` | Hare |
| `ForestMonsters` | Boar, Deer, Neck, Chicken, Hen, Greyling, Greydwarfs, Troll, Stone Golem |
| `Undead` | Skeletons, Draugr, Ghost, Wraith, Abomination, Blob, Oozer, Growth, Leech |
| `Demon` | Surtling, Asksvin, Bonemaw Serpent, Charred, Fallen Valkyrie, Morgen, Volture |
| `MountainMonsters` | Bat, Frost Blob, Fenring, Cultist, Drake, Ulv, Wolf |
| `SeaMonsters` | Serpent |
| `PlainsMonsters` | Deathsquito, Fulings, Fuling Berserker, Fuling Shaman, Lox |
| `Boss` | Eikthyr, The Elder, Bonemass, Moder, Yagluth, The Queen, Fader |
| `MistlandsMonsters` | Gjall, Seeker, Seeker Brood, Seeker Soldier, Tick |
| `Dverger` | Dvergr rogues and mages, Kvastur, Mistile |
| `PlayerSpawned` | Summoned Troll |
| `TrainingDummy` | No matching root prefab in the inspected core bundle |

Several rows cross biome or creature-type expectations. Boars, deer, necks,
chickens, and Stone Golems use `ForestMonsters`. Growth uses `Undead`. Hare is
the only inspected core prefab that uses `AnimalsVeg`. The prefab named
`TrainingDummy` uses `Undead`, not `TrainingDummy`.

## Directional Hostility

`BaseAI.IsEnemy(attacker, target)` is directional. It first applies group,
taming, and aggravation rules. It then uses the attacker's faction:

| Attacker faction | Different factions this attacker does not ordinarily treat as enemies |
| --- | --- |
| `Players` | `Dverger` |
| `AnimalsVeg` | None |
| `ForestMonsters` | `AnimalsVeg`, `Boss` |
| `Undead` | `Demon`, `Boss` |
| `Demon` | `Undead`, `Boss` |
| `MountainMonsters` | `Boss` |
| `SeaMonsters` | `Boss` |
| `PlainsMonsters` | `Boss` |
| `Boss` | Every faction except `Players` and `PlayerSpawned` |
| `MistlandsMonsters` | `AnimalsVeg`, `Boss` |
| `Dverger` | `AnimalsVeg`, `Boss`, `Players` |
| `PlayerSpawned` | None |
| `TrainingDummy` | Every faction except `Players` |

Characters in the same faction are not ordinary enemies. Characters with the
same non-empty `m_group` are also not enemies, even when their factions differ.

Tamed characters treat players, other tamed characters, and non-aggravated
Dverger as friendly. They treat other characters as enemies. Aggravation can
override the usual Dverger-player friendliness.

## Friendly Damage Is A Separate Rule

Faction hostility controls target selection and normal friendly-fire filters
in `Attack`, `Projectile`, and `Aoe`. Once a hit passes those filters and
reaches `Character.RPC_Damage()`, that method does not reject monster damage
because two creatures share a faction.

`Aoe` has explicit `m_hitEnemy`, `m_hitFriendly`, `m_hitSame`, `m_hitOwner`,
and `m_hitCharacters` controls. Its source defaults permit enemy and friendly
character hits, while excluding the owner and characters with the owner's
name. A serialized prefab can override those defaults.

A future effect can therefore damage nearby same-faction creatures without
changing Valheim's faction relationships. That effect must enable `m_hitSame`
to damage other characters whose name matches its owner's name.

For Methsquito, this means a native-style explosion can damage nearby Plains
creatures without changing how those creatures choose targets. The installed
prefab examples above verify Deathsquito, Fuling variants, and Lox as
`PlainsMonsters`. Inspect any other named target's prefab before relying on
this rule.

## Direct Source Evidence

| Question | Valheim `0.221.12` source |
| --- | --- |
| Faction identifiers and prefab field | `Character.Faction`, `Character.m_faction`, `GetFaction()` |
| Group override | `Character.m_group`, `GetGroup()`, `BaseAI.IsEnemy()` |
| Taming and aggravation | `BaseAI.IsEnemy()`, `BaseAI.IsAggravated()` |
| Attack filtering | `Attack`, `Projectile`, `Aoe.ShouldHit()` |
| Target-owner damage | `Character.Damage()`, `Character.RPC_Damage()` |
| Named creature examples | Installed SoftRef manifest and bundle `c4210710`; root prefab `Character.m_faction` values |
