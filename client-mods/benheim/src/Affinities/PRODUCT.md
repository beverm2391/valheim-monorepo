# Affinities

Affinities reward resource grind with powerful new weapon playstyles. A player
applies an affinity to an existing weapon, then uses that weapon in its intended
situation to gain an advantage through skilled combat.

An affinity is stronger than the native weapon in its intended situation and
less flexible outside it. An affinity is not a neutral modification whose
penalty cancels its reward. A substantial upfront grind for progression and
materials buys real power. The affinity's permanent bias keeps that power from
making every other weapon irrelevant.

Affinities should create new actions, physical interactions, or tactical
possibilities that existing weapons do not provide. An affinity cannot satisfy
this requirement only by reusing base-game elemental damage or another
existing weapon property. Useful options outside combat are desirable when
they arise naturally from the same mechanic. Players should be able to discover
creative uses rather than having those uses designed away.

## Permanent weapon specialization

An affinity modifies one existing weapon item. The weapon keeps its native
identity, quality, durability, upgrades, and ordinary repair behavior. The
affinity stays with that item through storage, dropping, trading, death,
logout, and server saves.

The MVP has these boundaries:

- Each eligible weapon has one affinity slot.
- Applying an affinity is permanent. Before applying it, the player must
  confirm that the choice cannot be reversed.
- The player cannot remove or replace an affinity.
- Affinities have fixed behavior. They do not roll random values.
- Affinities have no levels, XP, maintenance cost, or separate mastery tree.
- Uninstalling the Benheim mod leaves the weapon usable with its base-game
  behavior. Reinstalling Benheim restores the affinity's stored behavior.

## Acquisition and application

Defeating one designated boss unlocks the affinity system for the entire world.
This unlock happens only once. The exact boss remains open.

The player applies an affinity directly in a menu:

1. Select an eligible weapon from the inventory.
2. Select an affinity available for that weapon family.
3. Review its new behavior, permanent bias, and exact resource cost.
4. Confirm that the application cannot be reversed.
5. Apply the affinity, consuming the listed resources and permanently
   modifying the selected weapon.

There is no Sigil or other intermediate affinity item. Each affinity has its
own resource cost, which the menu consumes in full when the player applies it.
Materials from harder biomes gate normal progression through ordinary
exploration and resource gathering. The exact recipes and the physical station
that hosts the menu may vary as the system expands.

## Power creates a loadout choice

Every affinity must define five things:

- the weapon family that can receive it;
- the combat situation where it becomes exceptional;
- the power that rewards using it well;
- the permanent bias that makes the weapon worse outside that situation; and
- the feedback that makes the specialization readable.

The player cannot toggle the bias off to recover the native weapon whenever it
becomes inconvenient. A weapon with an affinity should give the player a reason
to prepare a loadout, switch weapons, and master a different combat rhythm.

## First playable slice: Lunge

The first playable slice applies Lunge to the base-game Club. This intentionally
narrow combination must prove the complete path before the system expands to
more weapons or affinities.

A Lunge Club propels the player forward once when the player performs an
airborne primary swing. The same physical behavior supports aggressive
gap-closing and creative traversal. Grounded swings retain the Club's base-game
behavior. The exact force and permanent combat drawback remain open. The
feasibility report informs these product decisions, but Ben and the Project
Lead must settle them and update this document before implementation proceeds.
The slice is not product-complete until Lunge has a meaningful advantage and a
felt loss of flexibility.

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
that Club, Lunge's new behavior, its permanent drawback, and the complete
resource cost. Before applying Lunge, the player must confirm that the change
is permanent. The application then revalidates the exact Club and listed
resources, consumes the cost once, and modifies that Club.

The Affinity tab must handle application separately from native crafting and
upgrading. Applying an affinity must not appear as a normal craft or upgrade or
change the existing Craft and Upgrade tabs. Support for other crafting stations
remains open.

## Developer testing and diagnostics

Only during development, the existing `bh debug` commands may bypass
player-facing costs and permanence to isolate failures:

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
The real acceptance path remains Forge, Affinity tab, resource cost,
confirmation, and permanent application.

Diagnostics must use distinct event types for menu discovery, eligibility,
application validation, resource consumption, writing and loading stored state,
and accepted or rejected Lunge attempts. A successful Lunge attempt records
velocity before the impulse, the applied impulse, and velocity afterward.

## Feasibility report before implementation

The product requirements describe the intended player outcome, not a promise
that the first technical design is correct. Before implementation begins, the
Dev Lead investigates the installed Valheim version and reports what is
possible, what is easy or difficult, which existing Valheim systems or extension
points can be reused, and what risks or tradeoffs each viable approach creates.
The report must cover:

- adding a third native-style Forge tab without breaking Craft or Upgrade;
- identifying one exact inventory item throughout selection and application;
- preserving affinity state through equip, storage, drop, trade, death,
  logout, and server save;
- keeping a weapon with an affinity usable when Benheim is absent and restoring
  the affinity's behavior when Benheim returns;
- applying propulsion to the correct player in multiplayer; and
- revalidating and consuming resources exactly once.

The report must recommend the smallest options that prove the complete player
path without hiding their limitations, but it does not change this product
contract or choose product behavior. Ben and the Project Lead combine the
technical evidence with the intended gameplay, make any new product decisions,
and update this document before implementation proceeds. Implementation
convenience must not silently weaken the settled progression, permanence,
creative utility, or native interface goals.

## Later candidate: Snipe

Snipe remains the first Bow candidate after the Lunge slice proves the system.
It applies to an existing base-game bow rather than creating a separate bow.
Drawing a Snipe bow always uses Snipe's deliberate long-range presentation and
handling. Snipe rewards precision and skilled headshots at long range, while
poor close-range flexibility provides its permanent bias. Its exact unlock,
recipe, scope presentation, range benefit, handling cost, and headshot behavior
remain open.

## Status

Affinities are in product design and feasibility investigation. No affinity
application, persistence, or combat behavior is implemented yet.
