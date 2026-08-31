# World Labels

World Labels makes existing signs and portals readable at a glance. Sign glow
and portal labels are always-active visual features while Benheim is loaded.
Neither feature has a setting or saved state.

## Current Behavior

- No World Labels behavior has passed live visual acceptance.

## In Development

- Existing sign letters use a soft, warm portal-amber glow. The wooden board
  does not glow. The effect has no point light, pulse, or text markup.
- A portal shows floating text above it in Valheim's native style. The text
  matches the portal's current tag exactly. An empty tag has no label. The
  label updates when the portal tag changes.
- Portal labels appear at distances of 30 meters or less when nothing blocks
  the player's view of the portal. Walls and closed structures block the
  labels.
- Sign glow and portal labels are client-only visuals. They do not write to the
  network, world, portal, or sign state.

A live test still must judge the glow on existing wooden signs. It must judge
label placement on existing wooden and stone portals. The test must also
confirm the 30-meter boundary, labels hidden by walls, empty tags without
labels, and label updates after a portal rename.
