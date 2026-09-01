# Wisp Echo

Wisp Echo is a Mistlands detection mead. It helps a prepared player read nearby
danger through the mist without turning terrain, buildings, or the whole map
transparent.

The mead uses Wisps, Sap, and Royal Jelly. Exact ingredient counts and the
crafting station remain open. The first tuning candidate lasts five minutes and
pulses about every three seconds. Each pulse searches roughly 40 meters around
the local player. It reveals each detected threat for about one second. These
values are starting points for play, not accepted balance.

## A pulse, not permanent vision

Each pulse takes one snapshot of nearby hostile creatures. A detected creature
appears as a blue or cyan silhouette through Mistlands particle mist. Solid
terrain, rocks, and building pieces still hide it. The pulse may show small
markers that point from the camera toward detected threats outside the current
view. It must not create map pins or a permanent minimap radar.

The snapshot freezes which creatures were detected for that pulse. The selected
creatures may continue to animate during the short reveal. Literal frozen-pose
capture is unnecessary unless live testing shows that moving silhouettes are
unreadable.

Wisp Echo uses Valheim's native hostility rules. It must not expose friendly
players, tamed creatures, or neutral creatures as threats. It runs locally for
the player who consumed the mead. The item itself should retain ordinary
Valheim behavior for storage, dropping, sharing, and consumption.

## First proof: render one hostile correctly

The full mead waits on one bounded render experiment through
[Developer Diagnostics](../DeveloperDiagnostics/PRODUCT.md):

1. Select one loaded hostile in the Mistlands.
2. Render its cyan silhouette for about one second.
3. Confirm that the silhouette remains visible through particle mist.
4. Confirm that terrain, a rock, and a building wall each hide it completely.
5. Confirm that the experiment removes every temporary renderer, material, HUD
   object, and runtime hook when it stops.

The experiment may catalog the selected creature's renderer hierarchy, the
mist material and render order, one cyan donor, and one HUD donor. It must not
implement the mead, recipe, persistence, or server registration.

If the silhouette cannot remain visible through particle mist while terrain,
rocks, and building pieces hide it, stop. Do not substitute an always-visible
wallhack or the native Demister, which removes mist instead of revealing
threats within it.

## Status

Source and runtime feasibility are complete. Valheim provides native status
effect timing, hostile enumeration, and item registration paths. Public source
provides useful rendering and HUD patterns, but no existing mod proves the
required Mistlands render ordering.

No Wisp Echo code, item, recipe, or render probe is implemented or installed.
The next development slice is the bounded single-hostile render experiment.
Only an installed build containing that experiment belongs in
`PRODUCT_REVIEW.md`.
