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

The player animation and item evidence came from the extracted native asset
bundle identified as `c4210710`, whose SHA-256 was
`2d1e17fa941213747868face6b8fb13e23332292454007255c42562119e31448`.
AssetRipper `1.3.14` exposed the serialized controller, clips, and item presets.
A different bundle hash also invalidates the asset conclusions below.

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

## Light-to-heavy chain branching

Valheim does not currently branch a primary chain into a secondary attack.
`Player.PlayerAttackInput()` evaluates Primary before Secondary. A Primary
edge starts `m_queuedAttackTimer`; a Secondary edge starts
`m_queuedSecondAttackTimer` and clears the Primary timer. Both buffers last
`0.5` seconds, but `Player.HaveQueuedChain()` considers only the Primary buffer
or Primary hold. `Humanoid.StartAttack()` consults that method before it knows
whether the requested attack is primary or secondary. A buffered Secondary can
therefore start after the current attack ends, but it cannot use the current
primary animation's authored `Chain` gate.

These source facts expose a bounded scheduling gap, not a missing combat
system. Proposed prototype: read the existing Secondary queue, give that intent
priority over a held Primary only while the current attack is a primary whose
`Attack.CanStartChainAttack()` is true, and call the normal
`Humanoid.StartAttack(..., secondaryAttack: true)`. The admission override
must exist only around that guarded branch attempt; globally making
`HaveQueuedChain()` true for Secondary would also loosen unrelated callers
that use the same predicate. No direct input polling or second timing window is
needed.

The one-handed sword family is the clean first probe. Serialized `SwordIron`
data uses `swing_longsword` with three Primary chain levels and the distinct
`sword_secondary` attack with zero chain levels. `Attack.Start()` inherits a
chain level only when the new attack has multiple chain levels and the prior
animation name matches. The sword heavy therefore starts its existing
secondary state and ends the light combo. Native code still owns its stamina
check and spend, durability, skill gain, attack geometry, hit event, and damage.

Assessment: this prototype is low-to-medium complexity. It needs focused proof
for a Secondary tap versus a held Primary, early buffering and expiry,
insufficient stamina, interruption, and one branch per accepted input. It
should remain sword-only until those cases prove that Secondary intent cannot
accidentally continue the light chain or start two heavies. Swords avoid
Benheim's current axe, pickaxe, projectile, and defense patches.

The inference is that this will feel like an authored branch because both
states and the gate are native. Only gameplay can prove the transition reads
cleanly; the serialized controller proves the available states and timing
seams, not the perceived blend.

## Jump and plunge boundary

Valheim permits an ordinary attack to start in the air: neither
`Humanoid.StartAttack()` nor `Attack.Start()` requires `IsOnGround()`. That is
not a jump attack. `Character.Jump()` is a separate owner-side physics action
that requires ground contact and rejects `InAttack()`. `Player.OnJump()` spends
jump stamina and clears the minor-action queue. While airborne, player attacks
also bypass their configured movement slowdown, although their rotation factor
still applies.

The native player controller separates locomotion into `Jump`, `Jump Loop`,
and `Jump End` states. Normal weapon attacks enter their ordinary attack states
from `AnyState`; starting one in the air replaces the readable jump pose with a
normal swing. Physical ground contact independently drives landing callbacks
and fall damage. It does not release a weapon hit or select a landing attack.

The native knife assets are the closest apparent exception, but they confirm
the boundary. Knives already map Secondary to `knife_secondary`, and the player
bundle contains `Knife JumpAttack` and `Knife Attack Leap` clips. Their authored
events drive speed, trail, and hit timing; neither clip invokes
`CharacterAnimEvent.Jump()` or `Land()`. They are existing leaping secondary
animations, not a preparatory jump, airborne hold, and landing-hit lifecycle.
Creature jump clips belong to different rigs and controllers. This report did
not establish that any is compatible with the player controller.

There is therefore no honest low-complexity player jump/plunge prototype in
Valheim `0.221.12`. Gating a damage bonus on `!IsOnGround()` would only reward
hopping. Combining native Jump physics with an ordinary or knife attack would
stack unrelated states without an authored landing transition. A true plunge
would need at least compatible player preparation, airborne, impact, miss, and
landing states plus explicit interruption and movement rules: an animation and
controller project, not a small Weapon Rhythm patch.

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

### Candidate: sword light-to-heavy branch

During the one-handed sword's native Primary chain gate, let a buffered
Secondary start the existing sword heavy. Primary continues the light chain;
from neutral, both controls keep their native meaning; the heavy ends the
combo.

This candidate has a narrow technical scope: it changes input arbitration at
the native gate and then delegates to the existing heavy. Gameplay must
establish its value. Its main failure modes are held Primary winning over
Secondary, a failed heavy silently continuing the light chain, a stale
Secondary starting after the window, and duplicate heavy starts.

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

## Unresolved product choices

The evidence does not decide:

- whether Ben wants the one-handed sword light-to-heavy branch as the next
  playable prototype;
- whether a later branch should preserve Secondary intent on resource failure
  or fall back to no attack;
- how spacing should normalize different attack origins and colliders;
- whether a perfect-defense counter belongs in Weapon Rhythm;
- whether any weapon has an existing pose that can support genuine charge; or
- whether a true jump/plunge attack is valuable enough to justify a player
  animation-controller project.

This report does not choose the next prototype. It shows that the sword branch
is a bounded input-arbitration experiment, while a jump/plunge feature requires
Ben to accept a larger animation-controller scope.

## Valheim 1.0 revalidation

This report proves only Valheim `0.221.12`. Before using these seams on Valheim
1.0, create a new source cache for the new assembly hash and recheck:

- `Version.CurrentVersion`;
- `PlayerController.FixedUpdate()` and `Player.SetControls()`;
- `Player.PlayerAttackInput()` and its queue durations;
- `Player.HaveQueuedChain()` and Primary-versus-Secondary evaluation order;
- `Humanoid.StartAttack()`, attack selection, and the per-swing clone;
- `Attack.Start()`, `Update()`, `OnAttackTrigger()`, and
  `CanStartChainAttack()`;
- `Character.Jump()`, `Player.OnJump()`, ground contact, and landing callbacks;
- `CharacterAnimEvent.Chain()`, `Hit()`, `OnAttackTrigger()`, `Jump()`,
  `Land()`, `DodgeMortal()`, `Speed()`, and `FreezeFrame()`;
- `ZSyncAnimation` trigger and speed replication;
- `Character.Damage()` and `RPC_Damage()` authority;
- `Humanoid.BlockAttack()` timed-block behavior;
- `Player.UpdateDodge()` invulnerability replication;
- the player animator's jump and sword states; and
- representative sword and knife attack presets and clips.

Changed or missing methods, fields, constants, events, or authority checks
invalidate the related conclusion. Revalidation must happen before a 1.0
prototype reuses this report.
