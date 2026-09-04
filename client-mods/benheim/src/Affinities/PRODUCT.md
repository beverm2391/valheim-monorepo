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

In the ordinary inventory, an affinity-bearing weapon appends the affinity name
to its native title, such as `Club · Lunge`. Its hover description preserves
the native weapon description and appends a short Affinity section that
explains the new behavior and persistent bias.

The MVP has these boundaries:

- Each eligible weapon has one affinity slot.
- The normal MVP requires a max-quality weapon because upgrading can erase
  its affinity.
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
- Applying an affinity does not roll random item stats. An affinity's combat
  behavior may include explicitly designed randomness.
- Affinities have no levels, XP, maintenance cost, or separate mastery tree.
- Uninstalling Benheim makes the stored affinity dormant, and the weapon uses
  its base-game behavior. Reinstalling Benheim restores the stored affinity and
  its behavior.

## Acquisition and application

The affinity system does not use a separate boss or world-state unlock. The
Affinity tab is available at the Forge. Each affinity requires materials from
its intended stage of progression. The player can apply it only after finding
those materials through ordinary exploration.

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
own fixed resource cost, which the menu consumes in full when the player
applies it. The affinity has the same cost for every eligible weapon.
Later-game weapons already cost more to upgrade to maximum quality. The player
can apply later affinities only after gathering their materials from harder
biomes. Every application and replacement happens at the Forge.

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

Lunge is the inexpensive starter affinity. Its final resource cost should use
early-game materials so players can apply it before obtaining the Feather
Cape. Later, Lunge and the Feather Cape form a stronger traversal loadout.

## Native Forge experience

The first slice adds an Affinity tab beside Valheim's Craft and Upgrade tabs at
the base-game Forge. It should look, sound, and navigate like the surrounding
Valheim interface. It reuses native-style weapon selection, item presentation,
resource rows, input behavior, and confirmation feedback rather than opening a
separate Benheim window.

The left panel uses the list layout from Valheim's native Craft tab. It shows
each unlocked weapon-and-affinity combination once, regardless of whether the
player currently carries a matching weapon or the required materials. A
combination appears after the player discovers its weapon type and the
materials required by its affinity. Test Affinity appears for every supported
weapon type the player has discovered.

Selecting a combination shows the affinity's behavior, persistent bias, and
complete resource cost. It also shows the exact weapon that will receive the
affinity. The player can apply the affinity only while carrying every required
material and an eligible weapon of the required quality. If the player carries
several matching weapons, the player can choose the exact weapon. Before
confirmation, the Forge must clearly identify the selected weapon because
affinity state belongs to that individual weapon.

For the first playable slice, the station requirement shows a level-1 Forge.
The next material slot shows Lunge's current resource cost. Before applying
Lunge to a Club that does not already have Lunge, the player must confirm that
the resources will not be refunded. If
Lunge replaces a different affinity, the player must also confirm that the old
affinity and all materials previously spent on it will be lost. The application
then revalidates the exact Club and listed resources, consumes the cost once,
and modifies that Club.

The Affinity tab must handle application separately from native crafting and
upgrading. Applying an affinity must not appear as a normal craft or upgrade or
change the existing Craft and Upgrade tabs.

## Built-in Test Affinity

The Affinity tab always includes `Test Affinity` for development and
troubleshooting. It costs `1 Wood` and works on a weapon of any native quality
within a supported affinity family. It adds no gameplay mechanic or persistent
bias. Its name and description must state that it has no gameplay power.

Test Affinity uses the same Forge flow as a normal affinity. It requires
confirmation, consumes its resource cost, persists on the item, rejects
reapplication, and can be replaced only by paying the new affinity's cost.
Normal builds include Test Affinity so developers can verify this shared flow
without changing a real affinity's max-quality requirement or resource cost.

## Developer testing and diagnostics

During development, two `bh debug` commands bypass the player-facing Forge
flow:

- `bh debug affinity apply <affinity>` applies the named affinity to an
  eligible equipped weapon. It ignores the Forge, resource cost, confirmation,
  max-quality requirement, and replacement restrictions.
- `bh debug affinity remove` removes only Benheim affinity state from the
  equipped weapon.

These commands are developer escape hatches, not an alternative progression
path. Developers use them to apply and remove affinities quickly during in-game
mechanic tests. Changes to tuning values or code require a new build.
Developers use Test Affinity to verify the shared Forge flow and real affinities
to verify progression and balance.

Diagnostics must use distinct event types for menu discovery, eligibility,
application validation, resource consumption, writing and loading stored state,
and accepted or rejected Lunge attempts. A successful Lunge attempt records
velocity before the impulse, the applied impulse, and velocity afterward.

## Next playable slice: Snipe

Snipe specializes an existing bow for deliberate long-range headshots. The first
test supports only a base-game Huntsman Bow. Support for every base-game bow
remains the intended expansion, not part of this slice. The
player applies Snipe through the existing Affinity tab at a level-1 Forge. Its
temporary resource cost is `1 Wood`. The shared application, persistence,
presentation, and same-affinity rejection rules apply.

Drawing a Snipe bow automatically gives the player 3x optical zoom. The zoom
changes field of view while preserving the native crosshair, third-person
camera position, and look sensitivity. The camera zooms in smoothly. A soft
vignette darkens the screen edges as draw progress increases, reaching its
strongest intensity at full draw while keeping the center clear. Snipe adds no
circular scope mask, scope toggle, or range predictor. The zoom and vignette
remain active when optional Bow Focus cosmetics are disabled.

Firing or canceling the draw clears the vignette and restores the normal field
of view almost instantly. This exit should feel immediate, not like a slow
return from aiming. The exact transition timing will be tuned by feel during
gameplay testing.

Drawing a Snipe bow takes 25% longer to reach full draw than the native bow,
after Valheim's skill adjustment. This applies at every range. Partial draws
and stamina use remain native. There is no additional movement penalty or flat
damage penalty.

A Snipe arrow's total headshot multiplier is 1.25x at distances up to 20 m. It
rises linearly to 2.25x at 60 m and stays at 2.25x beyond that distance. The
multiplier is 1.75x at 40 m. This replaces the ordinary Benheim headshot
multiplier rather than multiplying it again. Body shots, native WeakSpots, and
ammunition effects remain unchanged. The headshot benefit does not require a
full draw. An arrow keeps its Snipe behavior if the player switches weapons
before impact.

These values are approved starting tuning, not accepted gameplay balance.
Live review must establish that the zoom is useful, the slower draw creates a
felt close-range tradeoff, and long-range headshots reward precision. Final
resource costs remain open.

## Later candidate: Chain Lightning

Chain Lightning specializes a melee weapon for groups at the expense of direct
damage against isolated enemies. The weapon remains usable between chains and
when finishing survivors. Against clustered enemies, spreading lightning can
more than compensate for that disadvantage. Group control comes from spreading
damage and pressure; the candidate does not add separate stun or slow effects.

Each distinct enemy struck directly by one swing starts its own chain.
Lightning can jump and fork between nearby hostile creatures. Players improve
their chances by gathering enemies closely and striking several with one swing,
without controlling the exact path or outcome. Most activations should produce
a modest chain; some should grow into spectacular branching cascades.

Flying enemies are eligible. Landing a melee hit on a reachable enemy can send
lightning into nearby airborne enemies, including mixed ground-and-air groups.
The player does not need to reach every target with the weapon. This gives the
affinity a role against groups of Deathsquitos and flying Mistlands enemies,
without guaranteeing that every nearby enemy will be hit.

Each jump uses three-dimensional distance from the enemy it leaves. Random
target selection favors nearby enemies, and reach shrinks as the chain
progresses. Solid terrain and building pieces block jumps. A creature hit by
any branch cannot be hit again by that same chain.
Lightning hits continue their existing chain rather than starting
independent chains. Native damage ownership and resistance behavior remain
intact. A fast, followable sequence of effects shows successful jumps and forks.

The first test uses a max-quality Club. Direct damage at 75% of normal remains
a starting tuning candidate, not accepted balance. Exact jump damage, branch
probabilities, distance and continuation curves, and total-hit limits remain
open. Whether independent chains may share secondary targets, and how blocked
or dodged direct hits affect activation, also remain open. A ranged version is
outside this first slice. Implementation remains paused while these product
decisions are scoped.

## Status

The first Affinity slice is implemented as a playable candidate. Live `0.1.80`
testing accepted Lunge's movement and feel, the basic Forge-tab presentation,
and the disabled same-affinity action. That test used a max-quality base-game
Club, a Forge Affinity tab, versioned item state, a temporary cost of 1 Wood, and a
diagonal Lunge impulse. Static proof also covers:

- guards for the selected item and resources;
- rejection of the affinity already installed on the exact item;
- code paths that connect affinity state to native persistence;
- isolation of the Affinity tab;
- one Lunge per qualifying swing;
- additional Lunges from later airborne primary swings;
- isolation of developer commands; and
- separate diagnostic event types.

Live product review must still prove:

- the Forge level `1` requirement display and ordinary inventory title and
  hover description;
- the Affinity tab's input behavior, sounds, and restoration of the native
  Craft and Upgrade tabs;
- persistence through native item paths;
- movement ownership and smoothing in multiplayer;
- grounded Club swings remain native; and
- whether Valheim continues to treat the player as grounded for a short period
  after takeoff and, if so, whether that changes Lunge timing or feel.
