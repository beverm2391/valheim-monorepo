# Valheim Creature Mechanics

Valheim does not have one universal creature-behavior object. A creature fight
is composed from `Character`, `BaseAI`, `MonsterAI`, `Humanoid`, the creature's
inventory, its item-authored `Attack` records, and compatible animator and
effect assets. The control inventory below distinguishes prefab-authored values
from shared runtime behavior and synchronized state. Native stars derive a few
outcomes from `Character` level but do not select a different AI or attack
deck.

This is a version-scoped technical reference, not a product contract, tuning
proposal, or implementation plan. It owns the reusable creature-control model.
[Enemy Tiers research](../EnemyTiers/RESEARCH.md) owns star selection, spawn
context, Alpha identity, and retaliation evidence.

## Evidence Baseline

These findings apply to installed Valheim `0.221.12`. Its
`assembly_valheim.dll` had SHA-256
`ada4bd74e926680f7c17e1275b344ad2a9afd5759bf52146ad1a4c7866721b48`,
and `Version.CurrentVersion` reported `0.221.12`.

The control inventory was verified against ILSpy output from that assembly.
Installed asset manifests can prove that named assets exist, but not their
serialized component values. This reference therefore does not claim an
exhaustive prefab catalog or exact attack deck for any creature.

## How A Creature Chooses And Lands An Attack

1. The peer that owns the creature's `ZNetView` runs `MonsterAI.UpdateAI()`.
   It senses or remembers a target, chooses movement, and calls
   `SelectBestAttack()` when an attack may be useful.
2. `Humanoid.EquipBestWeapon()` filters the creature's inventory through
   `BaseAI.CanUseAttack()`, target type, range, interval, priority, and current
   creature state. `MonsterAI` then applies facing and angle readiness.
   Selection is among item-authored candidates; it is not a universal
   hard-coded combo list.
3. `MonsterAI.DoAttack()` checks the selected item again and calls
   `Character.StartAttack()`. `Humanoid.StartAttack()` shallow-clones the
   item's primary or secondary `Attack` and starts that transient clone.
4. `Attack.Start()` records the attacker, item, animation, and timing state.
   The compatible animation must later invoke `CharacterAnimEvent.Hit()` or
   `OnAttackTrigger()`.
5. `Humanoid.OnAttackTrigger()` forwards that animator event to the active
   `Attack`. `Attack.OnAttackTrigger()` dispatches to its melee, area, or
   projectile path and builds the swing's `HitData`.
6. `HitData` carries damage types, force, stagger, backstab, status effect,
   hit point and direction, skill data, and the attacker's `ZDOID`. Projectiles
   carry equivalent data into the projectile component before impact.
7. `Character.Damage()` routes the hit to the target's network owner.
   `Character.RPC_Damage()` on that owner resolves attacker identity,
   blocking, dodge and backstab rules, weak spots, resistances, health, stagger,
   and hit effects.
8. On lethal damage, the target owner creates death effects and any ragdoll,
   invokes `CharacterDrop` through the death callback, applies configured
   progression or boss side effects, and destroys the networked creature.

The important split is deliberate: the attacker owner decides and emits an
attack; the target owner authoritatively resolves its effect.

## Authority, Synchronization, And Persistence

| State family | Native boundary | Consequence for variants |
| --- | --- | --- |
| Identity and durable combat state | Level, health, maximum health, and selected AI flags such as alert, hunt-player, event-creature, sleeping, and day-despawn use the creature's ZDO. | Native replication and ownership transfer preserve these fields. Do not assume other fields are persisted because they exist on a component. |
| AI decisions | `BaseAI` and `MonsterAI` update only on the current creature owner. | Every peer that may own the creature must run compatible behavior. Server-only AI configuration is insufficient in a zone-owner model. |
| Prefab and runtime configuration | Speeds, senses, pursuit values, path-agent choice, attack-selection fields, `Attack` geometry, and effect lists are ordinary component or asset fields, not native per-creature ZDO state. | A level-derived variant can remain persistence-free only when compatible owners derive and apply the same values deterministically from synchronized identity. |
| A swing in flight | `HitData` serializes the attacker and attack result through the damage RPC; the target owner resolves it. | Attacker-side changes must produce equivalent `HitData`; target-side resistance, weak-spot, health, and stagger changes must be present on the resolving owner. |
| Death and drops | The creature owner runs death effects, drop generation, global-key changes, and network destruction. | Avoid parallel client-side drops or effects. Extra durable consequences need an explicit authoritative state design. |

`ItemDrop.ItemData.Clone()` is shallow for `m_shared`. `Attack.Clone()` is also
shallow. Mutating `ItemData.SharedData`, nested `Attack` data, or shared effect
arrays for one creature can therefore change every user of the same asset.
Per-creature work must own its mutable copies or keep variation outside those
shared objects.

## Factions And Creature Damage

Each `Character` prefab has an `m_faction` value. Valheim `0.221.12` defines
these factions:

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

The names are code identifiers, not a complete creature roster. Inspect a
creature's prefab before claiming which faction it uses.

`BaseAI.IsEnemy(attacker, target)` decides whether ordinary AI and attacks
treat one character as hostile. The relationship is directional. It first
applies group, taming, and aggravation rules. It then uses the attacker's
faction:

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
make Dverger and players hostile.

Faction hostility controls target selection and the normal friendly-fire
filters in `Attack`, `Projectile`, and `Aoe`. It is not a final immunity rule.
Once a hit passes those filters and reaches `Character.RPC_Damage()`, that
method does not reject monster damage because two creatures share a faction.

`Aoe` has explicit `m_hitEnemy`, `m_hitFriendly`, `m_hitSame`, `m_hitOwner`,
and `m_hitCharacters` controls. Its source defaults permit enemy and friendly
character hits, while excluding the owner and characters with the owner's
name. A serialized prefab can override those defaults. A future effect can
therefore damage nearby same-faction creatures without changing Valheim's
faction relationships. That effect must enable `m_hitSame` to damage other
characters whose name matches its owner's name.

## Control Inventory

Before changing a control, inspect current Benheim patches at the affected
native seam. This reference records authority and compatibility boundaries,
not a snapshot of the current patch inventory.

### Character: Body, Survivability, And Presentation

| Controls | Player-visible effect | Authorship and variation | Authority and compatibility boundary |
| --- | --- | --- | --- |
| Base/max health, regeneration | Time to kill and recovery | Base health is prefab-authored. Native level multiplies maximum health by level. | Health is owner-resolved and ZDO-backed. Any feature that changes level also composes with this scaling. |
| Damage modifiers, weak spots | Type resistance, immunity, vulnerability, and positional reward | Prefab components author the table and weak-spot colliders. Native level does not change them. | The target owner applies them in `RPC_Damage()`. Projectile-side `HitData` modifiers happen before this boundary and must compose with it. |
| Stagger factor and blocked-hit behavior | Stagger threshold and reactions | Prefab-authored; the absolute threshold rises when level-derived maximum health rises. | The target owner accumulates stagger. Any level-based health change also changes the absolute stagger threshold. |
| Walk/run/fly/swim speed, acceleration, turning, jump | Pace, closing speed, strafing feel, and turn commitment | Shared `Character` machinery with prefab values. Native level has no branch for these controls. | The owner drives movement. Creature-specific compatibility matters. |
| Radius, collider, visual scale, hit/death effects | Reachability, physical presence, readability, and impact | Collider and effects are prefab-authored. `LevelEffects` may scale its visual transform or swap material/object presentation for a level. | Visual scale does not prove matching collider, navmesh, or attack geometry. |

### BaseAI: Sensing And Navigation

| Controls | Player-visible effect | Authorship and variation | Authority and compatibility boundary |
| --- | --- | --- | --- |
| View range/angle, hearing range, mist vision | How early stealth, noise, facing, and cover matter | Generic sight/hearing tests use prefab values and target stealth/noise. Native level does not alter them. | The creature owner senses targets. Deterministic variants need compatible owners. |
| Path-agent type and walk/swim/water rules | Which terrain and passages the creature can traverse | Generic pathfinding consumes a prefab-selected agent family and movement capabilities. | Test each creature with its body and authored environment. Changing a path agent does not change its collider or animation. |
| Obstacle avoidance, move angle, smooth/serpent movement, stuck recovery | Cornering, local avoidance, and apparent navigation competence | Shared movement code combines prefab toggles with `Character` radius and speeds. | Owner-side and transient. Physical-scale or radius changes can invalidate otherwise sound avoidance. |
| Random movement, jump, flight altitude and takeoff | Idle movement and traversal style | Generic capabilities are enabled and tuned per prefab. | Animator and body compatibility are required. Native level supplies no automatic variation. |

### MonsterAI: Combat Decisions

| Controls | Player-visible effect | Authorship and variation | Authority and compatibility boundary |
| --- | --- | --- | --- |
| Alert range/state, hunt-player, target memory | Awareness, stealth pressure, and whether pursuit begins or continues | Generic logic, prefab ranges, and some ZDO-backed runtime flags. | Only the owner selects targets. |
| Maximum chase distance and unreachable-target timers | How far and how stubbornly a creature pursues | Prefab values feed shared timeout and spawn-point rules. | Owner-side. Changing these without navigation proof can create stuck or kited enemies. |
| Interception time | Whether pursuit leads a moving target | Generic velocity prediction with a prefab range sampled at startup. | Owner-side transient value; deterministic per-level behavior would need an explicit stable derivation. |
| Circling and circulate-while-charging | Repositioning instead of running directly at the target | Generic states enabled and timed per prefab, with separate flying support. | Animation, movement, attack range, and body shape constrain useful values. |
| Retreat/flee rules for hurt, low health, fire, lava, pheromone, or unreachable targets | Self-preservation, regrouping, and encounter rhythm | Shared branches controlled by prefab booleans and thresholds. | Owner-side; native level does not select different thresholds. |
| Minimum attack interval and charge/wait behavior | Overall cadence between eligible attacks | Creature-wide `MonsterAI` spacing composes with each item's own interval. | Owner-side. Any per-item interval change composes with this creature-wide floor. |

### Inventory Item: Attack Eligibility And Selection

`Humanoid.EquipBestWeapon()` treats inventory items as the creature's attack
options. `BaseAI.CanUseAttack()` and the item's shared AI fields can express:

- enemy, friend, or hurt-friend targets;
- minimum and maximum range, attack angle, and inverted-angle rules;
- per-item interval and prioritized selection;
- minimum and maximum health percentage, enabling health-phase attacks;
- dungeon-only or mist-only use;
- flying, altitude, walking, and swimming gates.

These are generic selectors over prefab-authored inventory. They can vary per
creature when its prefab has different items, and a future deterministic
variant could choose or filter options at runtime. Native stars do neither.
Because the fields live in shallow-shared item data, in-place per-creature
mutation is unsafe.

### Attack: One Swing's Execution

| Controls | Player-visible effect | Authorship and variation | Authority and compatibility boundary |
| --- | --- | --- | --- |
| Animation, chain, charge, loop, speed and rotation factors | Telegraph, commitment, combo shape, and turning during a swing | `Attack` records name animations; the controller and clip events must support them. | The attacker owner starts the clone. A field existing in `Attack` does not prove a creature has the required animation. |
| Origin, range, height, angle, ray width, hit-through-wall and multi-hit rules | Melee reach, arc, vertical coverage, and target count | Per-attack asset data processed by shared melee/area code. | Body scale and animation must agree with geometry. Native level does not alter it. |
| Damage types and multipliers, push force, stagger, backstab and status effect | Damage profile, knockback, stagger pressure, and debuffs | Per-attack values become `HitData`; native level multiplies attack damage by `1 + 0.5 * (level - 1)`. | Projectile-side `HitData` modifiers compose with this attack data before the target owner applies resistance and weak spots. Level does not change force or stagger multipliers. |
| Projectile prefab, count, bursts, velocity, accuracy, launch angle | Ranged pattern, spread, timing, and projectile behavior | `Attack` instantiates the configured projectile and calls its `IProjectile` seam. | The projectile/AOE prefab must implement the expected behavior and network ownership. Reusing numbers without the asset is not an attack. |
| Start, trigger, trail, burst and hit effects | Audio/visual telegraph and impact | Effect lists reference installed assets and optional attachment points. | The attacker or target owner creates effects at native seams. Asset existence does not prove semantic compatibility with another creature. |

### Death, Drops, And Level Signals

- `CharacterDrop` subscribes to the character's owner-side death callback.
  Each authored drop can enable `m_levelMultiplier`; when enabled, native level
  multiplies both chance and amount by `2^(level - 1)`, subject to native caps.
- `EnemyHud` shows level 2 and level 3 star objects. `LevelEffects` can apply an
  authored visual setup for a level. Neither system changes combat decisions.
- `Character` creates configured hit, critical, backstab, death, and ragdoll
  effects. Ragdolls may take over drop emission, so a second drop path would
  duplicate loot.

Any feature that changes native level also composes with the health, damage,
drop, HUD, and optional `LevelEffects` results above. Camera-only presentation
does not change creature death or drop authority.

## Boss, Miniboss, And Attack-Reuse Boundary

`Character.m_boss` selects boss HUD behavior. `m_dontHideBossHud` changes that
presentation. `m_bossEvent` can make the active boss override the current
random-event environment, and boss death may set a configured global key and
update the world-global active-boss count. Boss identity also affects the
`NoBossPortals` teleport gate, boss-kill statistics, and boss-specific status
effects from `Aoe`. These are not safe cosmetic labels: turning an ordinary
variant into a native boss can alter travel, event, progression, combat, and
statistics behavior.

The inspected assembly exposes no equivalent generic `miniboss` flag. A
miniboss-like encounter can still be authored from ordinary creature controls,
inventory, visuals, and location or event context, but the name is product
meaning rather than a proven native identity bit.

Boss and miniboss-style attacks remain asset-backed `ItemData`/`Attack`
compositions. Reuse is bounded by the destination creature's animator triggers
and clip events, skeleton and attachment points, projectile or AOE prefab,
effect assets, locomotion, and network behavior. This evidence supports
testing a named compatible attack. It does not support a universal attack
parser or automatic transplantation across creatures.

## Rules For Creature-Specific Follow-up

Add a creature-specific section here only when a named design decision needs
it. Inspect and record only the evidence needed for that creature:

1. prefab components, body scale/collider, path-agent type, and movement mode;
2. actual inventory items and their primary/secondary attack records;
3. animator triggers and the clip events that release those attacks;
4. projectile, AOE, attachment, and effect dependencies;
5. owner-side behavior and target-owner damage under multiplayer ownership;
6. interaction with the Benheim feature proposing the change.

Do not infer exact defaults from a field's declaration, copy another
creature's deck, or grow a full catalog preemptively. Preserve source or asset
evidence alongside each future named-creature claim.

## Reproduction Map

| Question | Direct evidence in `0.221.12` |
| --- | --- |
| Owner-side AI and sensing | `BaseAI.UpdateAI()`, `CanHearTarget()`, `CanSeeTarget()`, `MoveTo()`, `MoveAndAvoid()`; `MonsterAI.UpdateAI()`, `UpdateTarget()`, `SelectBestAttack()`, `DoAttack()` |
| Weapon selection and state gates | `Humanoid.EquipBestWeapon()`, `StartAttack()`; `BaseAI.CanUseAttack()`; `ItemDrop.ItemData`, `SharedData` |
| Attack cloning and animator release | `Attack.Clone()`, `Start()`, `OnAttackTrigger()`; `CharacterAnimEvent.Hit()`, `OnAttackTrigger()`; `Humanoid.OnAttackTrigger()` |
| Swing and projectile controls | `Attack.DoMeleeAttack()`, `DoAreaAttack()`, `FireProjectileBurst()`; `IProjectile.Setup()` |
| Hit synchronization and target authority | `HitData.Serialize()`, `Deserialize()`, `SetAttacker()`, `GetAttacker()`; `Character.Damage()`, `RPC_Damage()`, `ApplyDamage()` |
| Factions and friendly damage | `Character.Faction`, `GetFaction()`, `GetGroup()`; `BaseAI.IsEnemy()`; `Attack`, `Projectile`, and `Aoe.ShouldHit()` |
| Health, stagger, resistance, movement | `Character.SetupMaxHealth()`, `GetStaggerTreshold()`, `Stagger()`, movement update methods, `GetRadius()` |
| Navigation families | `Pathfinding.AgentType`, `HavePath()`, `GetPath()` |
| Death, drops, visuals | `Character.OnDeath()`; `CharacterDrop.OnDeath()`, `GenerateDropList()`; `EnemyHud.UpdateHuds()`; `LevelEffects.SetupLevelVisualization()`; `EffectList.Create()` |
| Boss side effects | `Character.IsBoss()`, `OnDeath()`; `EnemyHud`; `RandEventSystem.GetEnvOverride()`; `TeleportWorld`; `Aoe`; `ZoneSystem.SetGlobalKey()` |

Valheim 1.0 can change these seams. Revalidate the installed version and
assembly hash, owner gates, ZDO fields, attack-selection and animator path,
target-owner damage RPC, death/drop ownership, and any named creature assets
before relying on this reference after migration.
