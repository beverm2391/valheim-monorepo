# Weapon Affinities Research

Weapon affinities can create meaningful weapon roles without adding item
affixes or another combat framework. Valheim already carries damage type,
force, stagger, stamina, and status-effect behavior through its normal attack
and hit flow. A fixed Benheim rule can adjust those values at runtime while the
saved weapon remains a normal Valheim item.

The clean first direction is one deterministic role on one native weapon or
attack. This can prove whether preparation and weapon switching improve combat
before Benheim decides how many affinities should exist or how players receive
them.

## Evidence Baseline

These findings come from the installed Valheim assembly with:

- Valheim version `0.221.12`
- network version `36`
- `assembly_valheim.dll` SHA-256
  `ada4bd74e926680f7c17e1275b344ad2a9afd5759bf52146ad1a4c7866721b48`
- ILSpy `9.1.0.7988`

The assembly itself proves the version. `Version.CurrentVersion` constructs
`GameVersion(0, 221, 12)`, and `Version.m_networkVersion` is `36`.

Use the decompilation helpers documented in the root `PROMPT.md` to reproduce
the conclusions. The minimum useful types are `Version`, `ItemDrop.ItemData`,
`Attack`, `Humanoid`, `HitData`, `Character`, `CharacterAnimEvent`,
`ZSyncAnimation`, `SEMan`, and `Inventory`.

## Native Combat Model

`ItemDrop.ItemData.SharedData` owns a weapon's shared definition. The relevant
fields are:

- `m_damages` and `m_damagesPerLevel` for base and quality-scaled damage
- `m_attackForce` and `m_backstabBonus`
- `m_attackStatusEffect` and `m_attackStatusEffectChance`
- `m_attack` and `m_secondaryAttack`

`ItemDrop.ItemData.GetDamage()` combines the shared damage with quality and
world-level scaling. `HitData.DamageTypes` contains generic, blunt, slash,
pierce, chop, pickaxe, fire, frost, lightning, poison, and spirit damage.

`Humanoid.StartAttack(Character, bool)` selects the primary or secondary
`Attack` and calls `Attack.Clone()`. The clone is transient and becomes the
current attack. `Attack.Clone()` uses `MemberwiseClone()`, so primary and
secondary attacks share one model but keep separate definitions.

The most useful per-attack fields are:

- `m_attackStamina`
- `m_damageMultiplier`
- `m_forceMultiplier`
- `m_staggerMultiplier`
- `m_speedFactor` and `m_speedFactorRotation`
- attack range, angle, ray, chain, and projectile fields

`Attack.GetAttackStamina()` applies equipment, status-effect, and skill
modifiers to `m_attackStamina`. `Attack.Start()` checks the cost before the
attack begins, and `Attack.Update()` spends it when the attack starts.

`Attack.DoMeleeAttack()`, `Attack.DoAreaAttack()`, and
`Attack.FireProjectileBurst()` construct `HitData`. They combine the weapon's
damage, force, status effect, and skill data with the selected attack's damage,
force, and stagger multipliers. `Attack.ModifyDamage()` is the shared damage
step before `SEMan.ModifyAttack()` and native damage dispatch.

Knockback travels as `HitData.m_pushForce`. Stagger travels as
`HitData.m_staggerMultiplier`. `Character.ApplyDamage()` calculates stagger
from `HitData.DamageTypes.GetTotalStaggerDamage()` multiplied by the stagger
multiplier. Blunt, slash, pierce, and lightning damage contribute to native
stagger. Fire, frost, poison, spirit, chop, and pickaxe damage do not.

`Character.RPC_Damage()` applies the target's resistances before it routes
fire, spirit, poison, frost, and lightning through Valheim's native elemental
behavior. A damage-type exchange therefore creates real matchup differences
without a new effect system.

Weapon status effects are shared by default, not scoped to the primary or
secondary attack. Attack code writes the native status-effect hash into
`HitData.m_statusEffectHash`. The target owner resolves that hash through
`SEMan` and `ObjectDB` during `Character.RPC_Damage()`.

Attack speed has a different boundary. `Attack.m_speedFactor` and
`m_speedFactorRotation` control character movement and turning during the
attack. They do not change attack animation rate. Hit timing comes from the
animator and `CharacterAnimEvent.Hit()` or `OnAttackTrigger()`.
`ZSyncAnimation.SetSpeed()` changes the whole character animator, not one
weapon attack. Chains, interruptions, freeze frames, animation events, and
speed restoration all share that clock.

## Clean Fixed-Rule Seams

A fixed per-weapon rule can adjust a known item prefab's `SharedData` after
`ObjectDB` loads. A fixed per-attack rule can adjust that prefab's primary or
secondary `Attack` definition before Valheim clones it. Both approaches use
Valheim's normal stamina preflight, hit construction, and combat transport.
Shared damage changes also flow into the native weapon tooltip. Valheim does
not display every secondary-attack modifier, so some tradeoffs may need added
feedback.

A contextual rule can instead modify the transient `Attack` before
`Attack.Start()` validates its cost. This is still session-only, but it adds a
runtime hook that a fixed prefab rule does not need.

Damage types live on the shared weapon definition. A fixed weapon-wide
elemental exchange is therefore simple. A primary-only or secondary-only
exchange needs a targeted live-`HitData` hook, such as `Attack.ModifyDamage()`.
That remains protocol-free, but the private runtime method is more sensitive
to game updates.

Avoid character-global attack interception. A patch on `Character.Damage()` or
generic `IDestructible.Damage()` would mix player weapons with enemy attacks,
projectiles, gathering damage, and environmental hits.

Do not start with animation speed. The character-global animator is a much
less contained seam than damage, force, stagger, stamina, or attack movement.

## Persistence And Multiplayer Boundary

`Inventory.Save()` stores the item prefab name and normal instance fields such
as stack, durability, quality, variant, crafter, and custom data. It does not
store `SharedData`. `Inventory.Load()` resolves the prefab through
`ObjectDB.GetItemPrefab()` and reconstructs the item.

This makes a fixed affinity rule reconstructable on every launch. Removing
Benheim leaves a normal Valheim item with no affinity payload to migrate.
Selectable or rolled per-item affinities would need durable identity. They
cross the persistence boundary and remain outside this direction.

The attacker owner constructs the hit. `Character.Damage()` sends the native
`HitData` to the target owner through `RPC_Damage`. `HitData.Serialize()` and
`Deserialize()` already carry every native damage channel, push force, stagger
multiplier, status-effect hash, and relevant skill and item levels.

The target owner does not reconstruct the hit from the attacker's weapon. It
applies the values it receives. A fixed affinity therefore needs no new combat
message or dedicated-server component. Every attacker in the regular group
should still run compatible affinity rules for fair results and consistent
tooltips. A new custom status effect would be different because the receiving
owner must have that effect registered.

## Current Benheim Collision Risks

Benheim does not currently patch `Attack`, `Character.Damage`, `ItemDrop`,
`HitData`, `ZNet`, or `ZDO` for creature combat.

Archery is the important overlap. Its `Projectile.OnHit` transpiler modifies
the live hit's damage and stagger immediately before native damage dispatch.
A projectile affinity must define whether the headshot multiplier scales the
affinity and how stagger should compose. A melee-only first test avoids this
semantic collision.

Mining and Woodcutting modify or clone `HitData` only for gathering targets.
Adrenaline changes block and adrenaline paths. Loadout Swap uses native equip
and unequip actions without changing weapon stats. Pocket Items uses item
custom data for a separate manual preference; that use does not require
affinities to persist item data.

## Candidate Prototypes

### Breaker Secondary

The leading candidate is a secondary attack with more stagger and modestly
more force. Its cost would be higher stamina, lower direct damage, lower attack
movement, or a combination of those costs. It would give a weapon a clear role
against a stronger enemy whose dangerous sequence can be interrupted.

Pairing Breaker with a mace's native secondary is also only a candidate. The
chosen mace's prefab and secondary data still need direct verification. No
mace, modifier, enemy matchup, or feature has been approved. The pairing was
the cleanest candidate because it uses one existing attack and changes fields
that Valheim already evaluates per attack. It also avoids projectile and
animation-speed risks.

### Elemental Exchange

A fixed weapon could replace part of its physical damage with native fire,
frost, lightning, poison, or spirit damage while removing at least the same
physical budget. Target resistances would create matchup value. Fire, frost,
poison, and spirit would also reduce immediate stagger when they replace
physical damage. The weapon still needs readable native feedback so the result
does not feel arbitrary.

### Control Secondary

A secondary attack could gain modest reach, angle, or knockback in exchange for
more stamina, less damage, or worse attack movement. This is technically cheap,
but it is less matchup-specific than Breaker and can push enemies out of a
weapon's follow-up range.

## Decisions Still Open

The research does not approve an affinity or an implementation. Product work
still needs to choose:

- the first exact weapon and primary or secondary attack
- the stronger-enemy behavior that the weapon should counter
- the benefit and its meaningful cost
- whether the rule applies to one prefab or a native weapon family
- the feedback needed to make the affinity legible
- whether compatible versions also require identical balance configuration

Choose the enemy behavior and weapon pairing before choosing modifier values.
Do not generalize an affinity architecture until one small playable rule proves
that preparation and weapon switching improve combat.

Valheim 1.0 can change any private method, field, animation, prefab, or network
assumption in this report. Revalidate the assembly version, combat flow,
serialization, prefab data, and selected attack after the 1.0 migration and
before implementation.
