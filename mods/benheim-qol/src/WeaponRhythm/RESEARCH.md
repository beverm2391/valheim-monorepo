# Weapon Rhythm feasibility

Valheim already has a combat rhythm. Input selects an `Attack`, the animator
opens the combo gate, and an authored animation event creates the hit. Weapon
Rhythm should reward play at those native boundaries. It should not replace
them with a second combat state machine.

This report records feasibility evidence. It does not choose a mechanic,
timing window, reward, weapon, or balance value.

## Evidence baseline

The evidence comes from the installed macOS game and the current Benheim
source. The inspected game was Valheim `0.221.12`, Steam build `21981559`.
The inspected `assembly_valheim.dll` had SHA-256:

```text
ada4bd74e926680f7c17e1275b344ad2a9afd5759bf52146ad1a4c7866721b48
```

ILSpy `9.1.0.7988` produced the inspected source. The cache manifest recorded
the assembly hash and decompiler identity. [`PROMPT.md`](../../../../PROMPT.md)
owns the source inspection commands. To reproduce this evidence, cache the
installed assembly, confirm `Version.CurrentVersion`, and inspect the types
named below. A different assembly hash is a different evidence baseline.

## The animator owns the beat

The native flow is:

```text
PlayerController.FixedUpdate
  -> Player.SetControls
  -> Player.PlayerAttackInput
  -> Humanoid.StartAttack
  -> Attack.Start
  -> authored animation Hit event
  -> Attack.OnAttackTrigger
  -> target Damage RPC
```

`PlayerController.FixedUpdate()` reads pressed and held states for primary,
secondary, block, jump, and dodge. It sends them to `Player.SetControls()`.
The player owner then runs `Player.PlayerAttackInput()` from
`Player.FixedUpdate()`.

For ordinary weapons, `Player.PlayerAttackInput()` gives primary and secondary
presses separate `0.5` second input buffers. It calls
`Humanoid.StartAttack(target, secondaryAttack)` when the native state permits
the attack. `Humanoid.StartAttack()` rejects incompatible attack, dodge,
movement, stagger, and minor-action states. It then clones either
`ItemDrop.ItemData.SharedData.m_attack` or `m_secondaryAttack`.

`Attack.Start()` validates the selected attack and sends its animation trigger.
For native chains, it selects `m_attackAnimation` plus the chain level.
`CharacterAnimEvent.Chain()` opens the authored chain gate.
`Attack.CanStartChainAttack()` requires that gate and a nonzero
`m_nextAttackChainLevel`. A chain resets when the prior attack differs, the
configured level count ends, or `timeSinceLastAttack` exceeds `0.2` seconds.

The hit does not run when input arrives. `CharacterAnimEvent.Hit()` and
`CharacterAnimEvent.OnAttackTrigger()` call `Humanoid.OnAttackTrigger()`.
That owner-only method calls the current clone's `Attack.OnAttackTrigger()`.
The clone then performs its melee, area, projectile, or non-attack behavior.
The authored animation event is therefore the native hit window.

Stamina follows the same boundary. `Attack.Start()` checks the available
stamina. `Attack.Update()` spends it when the animator first enters the attack
tag. The public `Attack.m_speedFactor` and `m_speedFactorRotation` fields slow
movement and rotation during the attack. They do not change animation speed.

Primary and secondary attacks already have distinct presets. Each preset can
define its animation, stamina, geometry, damage, force, stagger, movement,
chain count, and other behavior. This is enough structure for real weapon
variation without new controls or animation assets.

## Clean extension seams

The cleanest timing seam is the per-swing clone passed to `Attack.Start()`.
After a successful start, a local-player postfix can change that clone's public
fields before the authored hit event reads them. Useful fields include:

- `m_damageMultiplier`
- `m_forceMultiplier`
- `m_staggerMultiplier`
- `m_speedFactor`
- `m_speedFactorRotation`

This seam leaves the shared item preset unchanged. It also preserves native
selection, stamina, animation, hit geometry, durability, skill gain, and damage
routing.

A cadence reward needs two observations. `Player.SetControls()` exposes the
actual attack edge. `CharacterAnimEvent.Chain()` exposes the weapon animation's
authored combo gate. A successful `Attack.Start()` can consume those ephemeral
timestamps and mark only the eligible clone. A generous band around the native
gate can reward timing without invalidating early buffered presses or ordinary
combos.

Spacing becomes known later. `Attack.DoMeleeAttack()` writes the impact to
`HitData.m_point` before calling the target's `Character.Damage()`. A narrowly
gated prefix on outgoing `Character.Damage()` can compare the local attacker,
active attack geometry, and impact point. It must exclude projectiles, chop,
pickaxe, self-damage, and unrelated `Character.Damage()` calls.

Perfect defense is another existing boundary. `Humanoid.BlockAttack()` treats
a block as timed when the blocker supports a timed bonus and `m_blockTimer` is
between `0` and `0.25` seconds. `Player.UpdateDodge()` starts the native dodge
animation and replicates `ZDOVars.s_dodgeinv`. `CharacterAnimEvent.DodgeMortal()`
ends invulnerability. Weapon Rhythm can observe these results without changing
their timing or authority.

## Authority and networking

The attacking player owns input, attack selection, animation-event hit
creation, and melee physics for their character. `ZSyncAnimation.SetTrigger()`
broadcasts the native animation trigger.

`Character.Damage()` serializes the completed `HitData` and sends
`RPC_Damage` to the target's ZDO owner. Only that owner performs the final
dodge, block, resistance, armor, and health checks. Damage, push, stagger,
impact point, direction, attacker, and skill data already cross this native
boundary.

An ephemeral cadence or spacing judgment can therefore remain attacker-local.
Its modified native `HitData` reaches the target owner without a server mod,
custom RPC, or persistent ZDO field. This model trusts each attacker. Every
active attacker must use a compatible Benheim version for shared combat
balance.

A later mechanic needs a versioned network contract only if it adds persistent
rhythm state, owner-side enforcement, or synchronized custom effects.

## Brittle boundaries

Animation speed is the wrong first lever. `ZSyncAnimation` writes the owner's
whole `Animator.speed` to the `anim_speed` ZDO field. Remote clients apply that
value. `CharacterAnimEvent.Speed()`, `FreezeFrame()`, and its normal reset logic
also own this value. Changing it shifts hit events, chain events, root motion,
attack-tag lifetime, and the relationship to the fixed input buffer. Stamina
still advances through the fixed update path.

Weapon Rhythm should not replace or manually call the hit event. Native
`Attack.OnAttackTrigger()` consumes ammo, chooses the correct attack path, and
applies related durability, effects, skill, and adrenaline behavior. Some
animations can contain more than one hit event.

Valheim has no generic player melee-charge seam. The player hold-and-release
path is the primary attack's bow flow. It uses `Attack.m_bowDraw`,
`m_drawDurationMin`, `m_drawStaminaDrain`, `m_drawEitrDrain`,
`m_drawAnimationState`, and the `drawpercent` animation parameter.
`Attack.m_chargeAnimationBool` belongs to `MonsterAI`, not the player path.
`m_loopingAttack` also depends on authored loop and `attack_abort` behavior.

A custom charged secondary could delay native attack start until release. It
would still lack a native preparatory pose for most melee weapons. Freezing or
respeeding the animator to fake that pose would cross the brittle boundary
above.

## Current Benheim interactions

Benheim currently has no patch on `Attack`, `Character.Damage`,
`ZSyncAnimation`, `Animator`, or melee movement.

The shortcut overlay patches `Player.TakeInput()` and `Menu.IsVisible()` to
block gameplay while the menu is open. Rhythm input should remain behind the
native control path. Direct Unity input polling would bypass that boundary.

Adrenaline observes `Humanoid.BlockAttack()` and
`Player.RPC_HitWhileDodging()` for perfect-defense feedback. It does not change
the native defense result. Its `Player.AddAdrenaline()` prefix doubles every
positive grant. A rhythm reward that grants adrenaline would inherit that
coupling.

Archery changes ranged `Projectile.OnHit()` behavior. Mining and woodcutting
change `MineRock`, `MineRock5`, `TreeBase`, and `TreeLog` damage. A generic
damage patch would collide with these features. An ordinary-melee
`Character.Damage()` seam must use explicit attacker, attack type, and damage
type gates.

Benheim disables gameplay actions when a required Harmony patch fails. A small
postfix on a public method is preferable to another instruction-indexed
transpiler.

## Bounded candidate prototypes

### Candidate: native-chain cadence accent

Choose one weapon family with a native multi-step primary chain. Reward a
generously timed use of its authored chain gate. Mark only the successful
per-swing clone and start with a modest stagger or push accent.

This candidate has the best gameplay-value-to-cost ratio. It preserves normal
attacks and lets each weapon's authored animation define its cadence. The main
failure modes are a window that taxes ordinary play, a large bonus that stacks
poorly with the native final-chain `2x` damage and `1.2x` push, and duplicate
rewards on multi-hit clips.

### Candidate: edge-of-reach spacing accent

Choose one reach weapon and reward contact near its effective outer range.
Prefer stagger, push, or another weapon-specific result before more damage.

This candidate could create strong weapon identity. Its risk is geometric
noise from attack origin joints, `m_hitPointtype`, large colliders, multi-target
swings, and terrain contacts. It should remain specific until gameplay proves
the distance measure.

### Candidate: perfect-defense secondary counter

After a native perfect parry or dodge, empower the next eligible secondary
attack for one weapon family. Apply the result through the per-swing clone.

This candidate reuses proven defense observations and existing secondary
animations. It may belong to Adrenaline or a defense system instead of Weapon
Rhythm. A global rule would also flatten weapon identity.

### Candidate: airborne normal-attack accent

Apply a small result to an eligible attack that starts in the air. Native
`Humanoid.StartAttack()` permits this, and airborne attacks bypass the normal
attack movement slowdown.

This is a cheap visual-feel probe, not evidence of a native jump attack. It
reuses the ordinary attack animation and could look wrong or reward repetitive
hopping.

## Unresolved product choices

The evidence does not decide:

- which weapon family should go first;
- whether cadence should reward damage, stagger, push, movement, or another
  result;
- how the cadence band should treat early native buffering;
- how spacing should normalize different attack origins and colliders;
- whether a perfect-defense counter belongs in Weapon Rhythm; or
- whether any weapon has an existing pose that can support genuine charge.

These are gameplay choices. The first candidate should answer one of them
without generalizing the system.

## Valheim 1.0 revalidation

This report proves only Valheim `0.221.12`. Before using these seams on Valheim
1.0, create a new source cache for the new assembly hash and recheck:

- `Version.CurrentVersion`;
- `PlayerController.FixedUpdate()` and `Player.SetControls()`;
- `Player.PlayerAttackInput()` and its queue durations;
- `Humanoid.StartAttack()` and the per-swing clone;
- `Attack.Start()`, `Update()`, `OnAttackTrigger()`, and
  `CanStartChainAttack()`;
- `CharacterAnimEvent.Chain()`, `Hit()`, `OnAttackTrigger()`,
  `DodgeMortal()`, `Speed()`, and `FreezeFrame()`;
- `ZSyncAnimation` trigger and speed replication;
- `Character.Damage()` and `RPC_Damage()` authority;
- `Humanoid.BlockAttack()` timed-block behavior; and
- `Player.UpdateDodge()` invulnerability replication.

Changed or missing methods, fields, constants, events, or authority checks
invalidate the related conclusion. Revalidation must happen before a 1.0
prototype reuses this report.
