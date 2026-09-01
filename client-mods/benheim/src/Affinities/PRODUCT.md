# Affinities

Affinities reward resource grind with powerful new weapon playstyles. A player
applies an affinity to an existing weapon, then uses that weapon in its intended
situation to gain an advantage through skilled combat.

An affinity is stronger than the native weapon in its intended situation and
less flexible outside it. An affinity is not a neutral modification whose
penalty cancels its reward. A substantial upfront grind for progression and
materials buys real power. The affinity's persistent bias keeps that power from
making every other weapon irrelevant.

Affinities should create new actions, physical interactions, or tactical
possibilities that existing weapons do not provide. An affinity cannot satisfy
this requirement only by reusing base-game elemental damage or another
existing weapon property. Useful options outside combat are desirable when
they arise naturally from the same mechanic. Players should be able to discover
creative uses rather than having those uses designed away.

## Persistent weapon specialization

An affinity modifies one existing weapon item. The weapon keeps its native
identity, quality, durability, upgrades, and ordinary repair behavior. The
affinity stays with that item through storage, dropping, trading, death,
logout, and server saves.

The MVP has these boundaries:

- Each eligible weapon has one affinity slot.
- Only a max-quality weapon can receive an affinity. Valheim upgrades replace
  the weapon item and do not preserve affinity data. Requiring max quality
  prevents a later upgrade from erasing the stored affinity, including while
  Benheim is uninstalled.
- The player applies every affinity at the Forge, regardless of the weapon's
  original crafting station.
- The player may replace an affinity at the Forge by paying the new affinity's
  full resource cost. The old affinity and all materials previously spent on
  that affinity are permanently lost.
- Applying the affinity already installed on the exact weapon is invalid. The
  menu visibly disables that choice. It opens no confirmation and consumes no
  resources. It does not modify the weapon or report a replacement attempt.
- The player cannot remove an affinity to recover a native weapon or toggle the
  affinity off in the field.
- Affinities have fixed behavior. They do not roll random values.
- Affinities have no levels, XP, maintenance cost, or separate mastery tree.
- Uninstalling Benheim makes the stored affinity dormant, and the weapon uses
  its base-game behavior. Reinstalling Benheim restores the stored affinity and
  its behavior.

## Acquisition and application

Defeating one designated boss unlocks the affinity system for the entire world.
This unlock happens only once. The exact boss remains open.

The player applies an affinity in the Affinity tab at the Forge:

1. Select an eligible weapon from the inventory.
2. Select an affinity available for that weapon family.
3. Review its new behavior, persistent bias, and exact resource cost.
4. Confirm the nonrefundable resource spend. If the weapon already has a
   different affinity, also confirm that the old affinity and all materials
   previously spent on it will be lost.
5. Apply the affinity at the Forge. This consumes the listed resources and
   stores the selected specialization until the player pays to replace it.

There is no Sigil or other intermediate affinity item. Each affinity has its
own resource cost, which the menu consumes in full when the player applies it.
Materials from harder biomes gate normal progression through ordinary
exploration and resource gathering. The exact recipes may vary as the system
expands. Every application and replacement happens at the Forge.

## Power creates a loadout choice

Every affinity must define five things:

- the weapon family that can receive it;
- the combat situation where it becomes exceptional;
- the power that rewards using it well;
- the persistent bias that makes the weapon worse outside that situation; and
- the feedback that makes the specialization readable.

The player cannot toggle the bias off to recover the native weapon whenever it
becomes inconvenient. A weapon with an affinity should give the player a reason
to prepare a loadout, switch weapons, and master a different combat rhythm.

## First playable slice: Lunge

The first playable slice applies Lunge to the base-game Club. This intentionally
narrow combination must prove the complete path before the system expands to
more weapons or affinities.

A Lunge Club performs a sharp diagonal air dash when the player performs an
airborne primary swing. It adds a 10 m/s forward impulse and raises vertical
velocity to at least +3 m/s. Normal gravity resumes immediately. After the first
Lunge, each later airborne primary swing can trigger one additional Lunge. This
intentional repetition supports aggressive gap-closing and creative traversal.
Grounded swings retain the Club's base-game behavior. The persistent combat
drawback remains open. The slice is not product-complete until Lunge has a
meaningful advantage and a felt loss of flexibility.

For this slice only, the normal boss unlock is absent and the resource cost is
a temporary testing recipe. This exception exists to test the system quickly;
it is not the final acquisition balance.

## Native Forge experience

The first slice adds an Affinity tab beside Valheim's Craft and Upgrade tabs at
the base-game Forge. It should look, sound, and navigate like the surrounding
Valheim interface. It reuses native-style weapon selection, item presentation,
resource rows, input behavior, and confirmation feedback rather than opening a
separate Benheim window.

The Affinity tab lists each eligible weapon item in the player's inventory and
the affinities available for that exact item. Selecting Lunge for a Club shows
that Club, Lunge's new behavior, its persistent drawback, and the complete
resource cost. Before applying Lunge to a Club that does not already have
Lunge, the player must confirm that the resources will not be refunded. If
Lunge replaces a different affinity, the player must also confirm that the old
affinity and all materials previously spent on it will be lost. The application
then revalidates the exact Club and listed resources, consumes the cost once,
and modifies that Club.

The Affinity tab must handle application separately from native crafting and
upgrading. Applying an affinity must not appear as a normal craft or upgrade or
change the existing Craft and Upgrade tabs.

## Developer testing and diagnostics

Only during development, the existing `bh debug` commands may bypass
player-facing resource costs, Forge use, and replacement restrictions to
isolate failures:

- `bh debug affinity inspect` reports the equipped weapon's eligibility,
  affinity identity and version, stored state, and active runtime behavior.
- `bh debug affinity apply lunge` applies Lunge to an equipped eligible Club
  without using the Forge, resources, or confirmation.
- `bh debug affinity clear` removes only Benheim affinity state from the
  equipped weapon so the same test can be repeated.
- `bh debug affinity lunge-force <value>` changes propulsion for the current
  session only. It does not modify the item or persist after the current
  session.

These commands are developer escape hatches, not alternate player progression.
The real acceptance path remains the Forge, the Affinity tab, full resource
cost, confirmation, persistent application, and paid replacement at the Forge.

Diagnostics must use distinct event types for menu discovery, eligibility,
application validation, resource consumption, writing and loading stored state,
and accepted or rejected Lunge attempts. A successful Lunge attempt records
velocity before the impulse, the applied impulse, and velocity afterward.

## Later candidate: Snipe

Snipe remains the first Bow candidate after the Lunge slice proves the system.
It applies to an existing base-game bow rather than creating a separate bow.
Drawing a Snipe bow always uses Snipe's deliberate long-range presentation and
handling. Snipe rewards precision and skilled headshots at long range, while
poor close-range flexibility provides its persistent bias. Its exact unlock,
recipe, scope presentation, range benefit, handling cost, and headshot behavior
remain open.

## Later candidate: Chain Lightning

Chain Lightning is the first melee candidate after Lunge. A successful direct
melee contact starts one readable sequence of lightning jumps through nearby
hostile creatures. The chain must not hit one creature twice, retrigger itself,
or jump through terrain and building pieces. It must preserve Valheim's native
damage ownership and resistance behavior.

The first tuning candidate reduces the weapon's direct damage to `75%`, then
jumps to as many as three additional targets within eight meters of the prior
target. These numbers are test inputs, not accepted balance. The eligible
weapon, lightning damage curve, blocking and dodging behavior, stagger, target
ordering, and final persistent bias remain open. A later ranged version may
reuse the product concept, but it is not part of the first melee candidate.

Research in the original source of public Valheim mods and the installed
Valheim runtime found no suitable mod to adopt as a dependency. Native Valheim
damage routing and chain effects provide the smallest implementation path.
During one Chain Lightning activation, the implementation must remember every
target already hit. It must prevent repeated targets and must not let generated
lightning hits start another chain.

## Status

The first Affinity slice is implemented as an unproven playable candidate. It
uses a max-quality base-game Club, a Forge Affinity tab, versioned item state, a
temporary cost of 1 Wood, and a diagonal Lunge impulse. Static proof covers
only:

- guards for the selected item and resources;
- rejection of the affinity already installed on the exact item;
- code paths that connect affinity state to native persistence;
- isolation of the Affinity tab;
- one Lunge per qualifying swing;
- additional Lunges from later airborne primary swings;
- isolation of developer commands; and
- separate diagnostic event types.

Live product review must still prove:

- the Affinity tab's layout, weapon list, affinity details, resource
  requirements, input behavior, sounds, and restoration of the native Craft
  and Upgrade tabs;
- persistence through native item paths;
- movement ownership and smoothing in multiplayer;
- whether the 10 m/s forward impulse and +3 m/s minimum vertical velocity
  create meaningful power with a felt loss of flexibility; and
- whether Valheim's short grounded grace period immediately after takeoff
  affects Lunge timing and feel.
