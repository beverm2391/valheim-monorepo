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

## Primary-to-secondary chain branching

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
system. A branch can read the existing Secondary queue, give that intent
priority over a held Primary only while an eligible current primary reports
`Attack.CanStartChainAttack()`, and then call the normal
`Humanoid.StartAttack(..., secondaryAttack: true)`. The admission override
must exist only around that guarded branch attempt; globally making
`HaveQueuedChain()` true for Secondary would also loosen unrelated callers
that use the same predicate. No direct input polling or second timing window is
needed.

The installed item presets and player controller do not support one uniform
"light to heavy" rule. The matrix below covers every current player melee
family and the individual exceptions that change branch feasibility. Damage
multipliers identify the native Secondary; they are evidence about weapon
meaning, not proposed tuning.

`Native Primary`, `Native Secondary`, and `Authored gate and transition`
report source evidence. `Honest branch result` and `Material boundary` are
technical assessments drawn from that evidence.

| Family | Native Primary | Native Secondary | Authored gate and transition | Honest branch result | Material boundary |
| --- | --- | --- | --- | --- | --- |
| One-handed swords | `swing_longsword`, three levels | `sword_secondary`, 3x thrust, zero levels | Levels one and two open `Chain`; a no-exit-time Any State transition blends in `0.1s` | Yes; Secondary ends the combo | Uniform across the inspected swords and elemental variants. The `0.4s` thrust is mechanically heavy but visually quick. |
| Greatswords | `greatsword`, three levels | `greatsword_secondary`, 3x thrust, zero levels | Levels one and two open `Chain`; no-exit-time `0.1s` transition | Yes; Secondary ends the combo | Clean but high-commitment: the Secondary clip is `2s` and inspected items cost `40` stamina. |
| Maces | `swing_longsword`, three levels | `mace_secondary`, 2.5x vertical strike, zero levels | Levels one and two open `Chain`; no-exit-time `0.1s` transition | Yes when the equipped item has the Secondary; it ends the combo | The `2.13s` overhead reads clearly. `Club` has no Secondary; `MaceWood` does and unusually costs only `4` stamina. |
| One-handed axes | `swing_axe`, three levels | `axe_secondary`, 1.5x vertical strike, zero levels | Levels one and two open `Chain`; no-exit-time `0.1s` transition | Yes; Secondary ends the combo | The Secondary clip contains a late `Chain` event, but zero configured levels make it inert. More frequent axe secondaries would also exercise Benheim Woodcutting on tree contacts. |
| Knives | `knife_stab`, three levels | `knife_secondary`, 3x leap, zero levels | Levels one and two open `Chain`; no-exit-time `0.1s` transition | Technically yes; Secondary ends the combo | The authored `Knife JumpAttack` is a movement commitment, not a stationary heavy. `KnifeButcher` has no Secondary. |
| Dual knives | `dual_knives`, three levels | `dual_knives_secondary`, 3x leap, zero levels | Levels one and two open `Chain`; no-exit-time `0.25s` transition | Technically yes; Secondary ends the combo | The longer blend and authored `Knife Attack Leap` preserve a deliberate lunge. Treating it as a generic heavy would obscure that identity. |
| Dual axes | `dualaxes`, four levels | `dualaxes_secondary`, 1.5x cleave, zero levels | Levels one through three open `Chain`; no-exit-time `0.1s` transition | Yes; Secondary ends the combo | Primary levels three and four each contain two `Hit` events. The level-three gate occurs after both hits, so an honest branch must not cut either short; branching also forgoes the two-hit finisher. Tree contacts remain in Woodcutting's domain. |
| Unarmed and claw weapons | `unarmed_attack`, two levels | `unarmed_kick`, 1x kick, zero levels | Level one opens `Chain`; no-exit-time `0.1s` transition | Yes; Secondary ends the combo | This is an authored punch-to-kick branch, not a heavy. Claw items reuse it but retain their own stamina and finisher values. |
| Battleaxes | `battleaxe_attack`, three levels | `battleaxe_secondary`, 0.5x quick strike, zero levels | The three Primary clips contain no `Chain` event; the Secondary has a no-exit-time `0.1s` transition | No native mid-attack gate; buffered Secondary already follows when the attack state ends | Forcing an earlier branch would invent timing and risk cutting off a hit. The quick Secondary is not a heavy, and tree contacts remain in Woodcutting's domain. |
| Atgeirs and polearms | `atgeir_attack`, three levels | `atgeir_secondary`, 1x 360-degree sweep, zero levels | The three Primary clips contain no `Chain` event; the Secondary has a no-exit-time `0.1s` transition | No native mid-attack gate; buffered Secondary already follows when the attack state ends | An early spin would require a new timing rule and change the weapon's reach-control identity. |
| Spears | `spear_poke`, zero chain levels | `spear_throw`, zero levels | No Primary chain gate | No | The chitin harpoon is stricter: throw is its Primary and it has no Secondary. Branching would change its ranged intent. |
| Two-handed clubs and sledges | `swing_sledge`, zero chain levels | None | No Primary chain gate or Secondary state | No | Their single area slam is already the whole authored attack. |
| Tool-like melee | Pickaxes and the scythe have zero levels; the torch has a three-level club swing | None | No usable Primary-to-Secondary pair | No | These are mining, farming, or utility actions, not a missing combat branch. |

Every Secondary listed in the matrix has zero chain levels. `Attack.Update()`
therefore sets its next chain level back to zero even if a clip emits `Chain`.
A native-only branch ends the combo. Returning directly to a light would
require custom combo state; a later Primary after the Secondary finishes is
simply a new native chain at level zero.

The common failure cases are also native. `Attack.Start()` checks Secondary
stamina before it changes animation and spends stamina only after the animator
enters the attack state. A failed start leaves the Secondary buffer alive until
its `0.5s` expiry, while a held Primary is still evaluated first on every
fixed update. Any prototype must prevent that held Primary from winning an
eligible branch, consume at most one accepted Secondary, and explicitly choose
whether a resource failure preserves the buffered Secondary or yields no
attack. It must not silently continue the light chain.

The cleanest first probe remains one-handed swords, now based on the complete
matrix rather than a sword-only sample. Their presets are uniform, the branch
has one hit and a short `0.1s` controller blend, and no current Benheim feature
patches their attack or target path. Greatswords and qualifying maces are the
next-cleanest probes and make the heavy more visually readable, at the cost of
longer commitment and more resource-failure exposure. Axes, knives, dual
weapons, and unarmed are technically branchable but carry the identity or
multi-hit risks named above. Battleaxes, atgeirs, spears, sledges, and tools do
not fit the requested native-gate prototype.

That ranking is a technical recommendation, not a product decision. The
inference is that a native gate plus native Any State transition will look
authored. Only gameplay can prove that the blend reads cleanly or that adding
the choice improves a family's rhythm.

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
Valheim `0.221.12`. An off-ground descending-contact rule without an approach
threshold would still reward hopping. Perfect Impact adds that threshold, but
it does not become a true jump or plunge attack. It can remain a small approach
tool because it combines native jump physics, authored melee contact, and the
existing stagger result without adding a landing lifecycle. A true plunge
would still need compatible player preparation, airborne, impact, miss, and
landing states plus explicit interruption and movement rules: an animation and
controller project, not a small Weapon Rhythm patch.

`Character.GetVelocity()` is the canonical velocity source at contact. It
returns live Rigidbody velocity for the owning character and replicated
velocity for a remote character. Perfect Impact reads the local owner's
velocity at the authored hit call that creates the outgoing `HitData`. It
combines the vertical component with native `IsOnGround()` and does not infer
the jump phase from a timer or stored state.

The serialized Player prefab sets walk speed to `1.6 m/s`, ordinary movement
speed to `4 m/s`, and sprint speed to `7 m/s`. A jump adds `2 m/s` forward,
scaled by Jump skill from `1x` to `1.4x`. An ordinary jog-jump therefore carries
about `6 m/s` at base skill and at most `6.8 m/s` at maximum skill, while a base
sprint-jump starts near `9 m/s`. A first-playtest threshold of `7 m/s` separates
those native bands without checking whether the sprint button is held.

The approach measure projects the attacker's planar `GetVelocity()` onto the
planar direction from the attacker to `HitData.m_point`. Native horizontal and
vertical melee write that point from the resolved collider contact. Native area
melee uses the collider's closest point. Using the authored contact point gives
large and multi-collider targets a stable approach direction. Sideways and
backward momentum cannot qualify. A
status or equipment effect that carries the required physical momentum can
qualify without sprint input because the rule measures movement, not intent.

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

Ben's `0.1.66` gameplay disproved the previous attack-start gate. Tested
airborne inputs were rejected or buffered until the player landed. The current
candidate therefore makes every qualification decision at `Character` contact.

The current experimental Perfect Impact implementation redirects only the
direct damage calls in `Attack.DoMeleeAttack()` and the generated area-hit
routine. Valheim clones an `Attack` before `Attack.Start()` and retains that
clone through the animator-triggered hit. Benheim uses that native identity to
keep one outcome per attack. It does not use attack-start movement or ground
state to qualify the mechanic.

At the first authored `Character` contact, Benheim reads native ground state and
physical velocity. The player must be airborne, descend at or below `-0.5 m/s`,
and meet the configured planar speed threshold toward `HitData.m_point`. A
qualified contact applies the experimental `1.15x` damage and `3x` stagger
multipliers. Later contacts cannot create or reverse the attack's first outcome.
A later target receives the modifiers only if the first contact qualified and
the later contact independently meets the same physical conditions.

A qualified swing requests `PERFECT IMPACT` through the existing native world
text helper, anchored at the struck `Character` and contact point, and requests
the existing Combat Feedback shake controller. For an area or multi-target
swing, the native per-target hit path and target-owner authority remain in
place. Benheim emits one confirmation request, one shake request, and one typed
outcome for the attack. The text is semantic gameplay feedback. Only the shake
follows the Benheim FX and Combat Shake settings.

The successful `Humanoid.StartAttack()` postfix captures only the native clone's
identity, weapon, and primary-or-secondary selection. It does not inspect
private input fields or decide eligibility. The `perfect_impact_outcome` event
records:

1. operation ID, attack identity, and target
2. qualification reason and ground state
3. velocity measurements and thresholds
4. damage and stagger multipliers
5. whether feedback was requested and the native feedback seam used

A live qualified Lox contact proved the damage and stagger result but no text
was visible even though the top-left caller reported `feedback=placed`.
Perfect Impact therefore no longer uses or reports placement from that lane.
Its diagnostic records only the feedback request and `native_world_text` seam;
neither field claims human-visible presentation. Grouped utility receipts keep
using the top-left lane. Gameplay still must prove the native world text is
visible.

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
damage patch would collide with these features. Perfect Impact instead changes
only outgoing native melee hits whose resolved target is a `Character`.

Benheim disables gameplay actions when a required Harmony patch fails. A small
call-site transpiler must assert the exact native damage-call count and fail
closed if Valheim changes that seam.

## Bounded candidate prototypes

### Candidate: one-handed sword Primary-to-Secondary branch

During the one-handed sword's native Primary chain gate, let a buffered
Secondary start the existing sword thrust. Primary continues the light chain;
from neutral, both controls keep their native meaning; the Secondary ends the
combo.

This candidate has a narrow technical scope: it changes input arbitration at
the native gate and then delegates to the existing Secondary. Gameplay must
establish its value. Its main failure modes are held Primary winning over
Secondary, a failed Secondary silently continuing the light chain, a stale
Secondary starting after the window, and duplicate Secondary starts.

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

- whether Ben wants the one-handed sword Primary-to-Secondary branch as the next
  playable prototype;
- whether a later branch should preserve Secondary intent on resource failure
  or fall back to no attack;
- how spacing should normalize different attack origins and colliders;
- whether a perfect-defense counter belongs in Weapon Rhythm;
- whether any weapon has an existing pose that can support genuine charge; or
- whether a true jump/plunge attack is valuable enough to justify a player
  animation-controller project.

This report does not choose the next prototype. It ranks the one-handed sword
branch as the narrowest identified native-gate seam, identifies the families
that do not expose the required gate, and keeps jump/plunge behind a larger
animation-controller decision.

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
- the player animator's jump and melee states; and
- each candidate family's Primary and Secondary presets, chain events,
  transitions, and branch-affecting item exceptions.

Changed or missing methods, fields, constants, events, or authority checks
invalidate the related conclusion. Revalidation must happen before a 1.0
prototype reuses this report.
