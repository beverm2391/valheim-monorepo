# World Labels

World Labels makes existing signs and portals readable at a glance. Sign glow
and portal labels are always-active visual features while Benheim is loaded.
Neither feature has a setting or saved state.

## Current Behavior

- Letters on existing wooden signs glow with a soft, warm portal-amber effect.
  The wooden board does not glow. The glow is static and adds no light source.
  It does not change the sign text.

- Each wooden or stone portal with a non-empty tag shows Valheim's wooden sign
  board floating 20 to 30 centimeters above the portal. The board stays fixed
  to the portal's position and rotation. It does not turn toward the camera.
- The board shows the portal tag on both sides with the existing glowing
  letters used on signs. The displayed text exactly matches the current portal
  tag and updates when the tag changes. A portal with an empty tag shows no
  board.
- The board behaves like scene geometry. Walls and structures hide it, and it
  does not appear through them as overlay text.
- Sign glow and portal labels are client-only visuals. They do not write to the
  network, world, portal, or sign state.

Ben accepted the wooden sign style and placement in live play.

## In Development

Some portal tags wrap below the wooden board. The complete tag must fit inside
the board on both sides, with space around the letters. Text fitting must keep
the accepted board style and placement. Live review must confirm readable tags
without overflow, including after a tag is changed.
