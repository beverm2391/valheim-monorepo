# World Labels

World Labels makes existing signs and portals readable at a glance. Sign glow
and portal labels are always-active visual features while Benheim is loaded.
Neither feature has a setting or saved state.

## Current Behavior

- Letters on existing wooden signs glow with a soft, warm portal-amber effect.
  The wooden board does not glow. The glow is static and adds no light source.
  It does not change the sign text.

## In Development

- A portal shows floating text above it in Valheim's native style. The text
  matches the portal's current tag exactly. An empty tag has no label. The
  label updates when the portal tag changes.
- Portal labels appear at distances of 30 meters or less when nothing blocks
  the player's view of the portal. Walls and closed structures block the
  labels.
- Sign glow and portal labels are client-only visuals. They do not write to the
  network, world, portal, or sign state.

The remaining live test must judge label placement on existing wooden and stone
portals. It must also confirm that:

- labels are visible at 30 meters but not beyond;
- walls hide labels;
- empty tags have no labels; and
- labels update after a player renames a portal.
