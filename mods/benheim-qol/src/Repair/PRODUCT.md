# Repair

The Repair module removes repeated repair clicks.

## Current Behavior

- A normal station repair click keeps Valheim's one-item behavior.
- `Left Shift` + station repair click repairs all eligible gear.
- A normal hammer repair click keeps Valheim's one-piece behavior.

## In Development

- Mass repair for buildings and structures does not currently work and needs
  debugging.
- `Left Shift` + hammer repair click will repair up to 80 eligible damaged
  pieces within 20 meters.
- Keep the repaired total in the normal top-left message feed. When mass repair
  repairs more than one piece, also show `N pieces repaired` in yellow over the
  originally targeted piece. Do not add this feedback to normal or single-piece
  repairs.
