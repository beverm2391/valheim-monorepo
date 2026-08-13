# Valheim Creature Mechanics

Valheim does not have one universal creature-behavior object. A creature fight
is composed from `Character`, `BaseAI`, `MonsterAI`, `Humanoid`, the creature's
inventory, its item-authored `Attack` records, and compatible animator and
effect assets. Native stars derive a few outcomes from `Character` level but do
not select a different AI or attack deck.

This is a version-scoped technical reference, not a product contract, tuning
proposal, or implementation plan. It owns the shared creature-control model.
Use these focused references for two mechanics:

- [Factions](FACTIONS.md) covers hostility, groups, taming, aggravation, and
  friendly damage.
- [Behavior](BEHAVIOR.md) covers sensing, navigation, pursuit, positioning,
  retreat, and attack spacing.

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
3. `MonsterAI.DoAttack()` checks the selected item and calls
   `Character.StartAttack()`. `Humanoid.StartAttack()` shallow-clones the
   item's primary or secondary `Attack` and starts that transient clone.
4. `Attack.Start()` records the attacker, item, animation, and timing state.
   The compatible animation must later invoke `CharacterAnimEvent.Hit()` or
   `OnAttackTrigger()`.
5. `Humanoid.OnAttackTrigger()` forwards that event to the active `Attack`.
   `Attack.OnAttackTrigger()` dispatches to its melee, area, or projectile path
   and builds the swing's `HitData`.
6. `HitData` carries damage, force, stagger, backstab, status effect, hit point
   and direction, skill data, and the attacker's `ZDOID`.
7. `Character.Damage()` routes the hit to the target's network owner.
   `Character.RPC_Damage()` resolves blocking, dodge, weak spots, resistances,
   health, stagger, and hit effects.
8. On lethal damage, the target owner creates death effects and any ragdoll,
   invokes `CharacterDrop`, applies configured progression or boss side
   effects, and destroys the networked creature.

The attacker owner decides and emits an attack. The target owner resolves its
effect.

## Authority, Synchronization, And Persistence

| State family | Native boundary | Consequence for variants |
| --- | --- | --- |
| Identity and durable combat state | Level, health, maximum health, and selected AI flags use the creature's ZDO. | Native replication and ownership transfer preserve these fields. Do not assume other fields persist because they exist on a component. |
| AI decisions | `BaseAI` and `MonsterAI` update only on the current creature owner. | Every peer that may own the creature must run compatible behavior. Server-only AI configuration is insufficient in a zone-owner model. |
| Prefab and runtime configuration | Speeds, senses, pursuit values, path-agent choice, attack-selection fields, attack geometry, and effect lists are ordinary component or asset fields. | A variant can remain persistence-free only when compatible owners derive the same values from synchronized identity. |
| A swing in flight | `HitData` serializes the attacker and attack result through the damage RPC. | Attacker-side changes must produce equivalent hit data. Target-side resistance, weak-spot, health, and stagger changes must exist on the resolving owner. |
| Death and drops | The creature owner runs death effects, drop generation, progression changes, and network destruction. | Avoid parallel client-side drops or effects. Extra durable consequences need an explicit authoritative design. |

`ItemDrop.ItemData.Clone()` is shallow for `m_shared`. `Attack.Clone()` is also
shallow. Mutating shared item, attack, or effect data for one creature can
change every user of the same asset. Per-creature work must own its mutable
copies or keep variation outside those objects.

## Remaining Control Inventory

Behavior and faction controls live in their focused references. The remaining
controls describe the creature's body, attack content, and results.

### Character: Body, Survivability, And Presentation

| Controls | Player-visible effect | Authorship and variation | Authority and compatibility boundary |
| --- | --- | --- | --- |
| Base and maximum health, regeneration | Time to kill and recovery | Base health is prefab-authored. Native level multiplies maximum health by level. | Health is owner-resolved and ZDO-backed. A level change composes with this scaling. |
| Damage modifiers and weak spots | Type resistance, immunity, vulnerability, and positional reward | Prefab components author the table and weak-spot colliders. Native level does not change them. | The target owner applies them in `RPC_Damage()`. Attacker-side hit modifiers happen before this boundary. |
| Stagger factor and blocked-hit behavior | Stagger threshold and reactions | The absolute threshold is maximum health multiplied by the prefab's stagger factor. | The target owner accumulates stagger. Level-derived health also raises the absolute threshold. |
| Walk, run, fly, and swim speed; acceleration; turning; jump | Pace, closing speed, strafing feel, and turn commitment | Shared `Character` machinery uses prefab values. Native level has no branch for them. | The owner drives movement. Creature body and animation compatibility still matter. |
| Radius, collider, visual scale, hit and death effects | Physical presence, readability, and impact | Collider and effects are prefab-authored. `LevelEffects` can change visual scale, material, or enabled objects. | Visual scale does not prove matching collider, pathfinding, or attack geometry. |

### Inventory Item: Attack Eligibility And Selection

`Humanoid.EquipBestWeapon()` treats inventory items as attack options.
`BaseAI.CanUseAttack()` and the item's shared AI fields can express:

- enemy, friend, or hurt-friend targets;
- minimum and maximum range, attack angle, and inverted-angle rules;
- per-item interval and prioritized selection;
- minimum and maximum health percentage for phase-like attack gates;
- dungeon-only or mist-only use; and
- flying, altitude, walking, and swimming requirements.

These selectors are generic, but the available items are prefab-authored.
Native stars do not add an item or change a selector. Because the fields live
in shallow-shared item data, in-place per-creature mutation is unsafe.

### Attack: One Swing's Execution

| Controls | Player-visible effect | Authorship and variation | Authority and compatibility boundary |
| --- | --- | --- | --- |
| Animation, chain, charge, loop, movement, and rotation factors | Telegraph, commitment, combo shape, and turning during a swing | The `Attack` record names animations. Its controller and clips must support those animations. | The attacker owner starts the transient clone. A field does not prove the animation exists. |
| Origin, range, height, angle, ray width, walls, and multi-hit rules | Melee reach, arc, vertical coverage, and target count | Per-attack data is processed by shared melee and area code. | Body scale and animation must agree with the geometry. |
| Damage, push, stagger, backstab, and status effect | Damage profile, knockback, stagger pressure, and debuffs | Per-attack values become `HitData`. Native level multiplies damage by `1 + 0.5 * (level - 1)`. | The target owner later applies resistance and weak spots. Level does not change force or stagger multipliers. |
| Projectile, bursts, velocity, accuracy, and launch angle | Ranged pattern, spread, timing, and projectile behavior | The attack instantiates a configured projectile through `IProjectile`. | The projectile or AOE prefab must support the expected ownership and behavior. |
| Start, trigger, trail, burst, and hit effects | Audio and visual telegraph and impact | Effect lists reference installed assets and optional attachment points. | Asset existence does not prove compatibility with another creature. |

### Death, Drops, And Level Signals

- `CharacterDrop` subscribes to the owner-side death callback. An authored drop
  can enable `m_levelMultiplier`; native level then multiplies chance and amount
  by `2^(level - 1)`, subject to native caps.
- `EnemyHud` shows native star objects. `LevelEffects` can apply an authored
  visual setup for a level. Neither system changes behavior or attacks.
- `Character` creates configured hit, critical, backstab, death, and ragdoll
  effects. A ragdoll can take over drop emission, so a second drop path can
  duplicate loot.

## Boss, Miniboss, And Attack-Reuse Boundary

`Character.m_boss` changes more than the HUD. Boss identity can affect event
environment, global progression keys, active-boss counts, portal rules,
statistics, and boss-specific status effects. Do not use the boss flag as a
cosmetic label for an ordinary variant.

The inspected assembly exposes no generic miniboss flag. A miniboss-like fight
can use ordinary creature controls, inventory, visuals, and encounter context.

Boss-style attacks remain asset-backed item and attack compositions. Reuse
depends on the destination creature's animator triggers, clip events, skeleton,
attachment points, projectile or AOE prefab, effects, locomotion, and network
behavior. This supports testing one named compatible move. It does not support
automatic attack transplantation across creatures.

## Creature-Specific Follow-up

Add creature-specific research only when a named design decision needs it.
Inspect only the evidence required for that creature:

1. prefab components, body scale, collider, path-agent type, and movement mode;
2. actual inventory items and their primary and secondary attacks;
3. animator triggers and clip events that release those attacks;
4. projectile, AOE, attachment, and effect dependencies;
5. owner-side behavior and target-owner damage in multiplayer; and
6. interaction with the Benheim feature proposing the change.

Do not infer exact defaults from a field declaration, copy another creature's
attack deck, or grow a complete prefab catalog without a product question.

Valheim 1.0 can change these seams. Revalidate the installed assembly, owner
gates, synchronized fields, attack and animator flow, target-owner damage,
death and drop ownership, and every named asset after migration.
