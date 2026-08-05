# Repair

The Repair module removes repeated repair clicks.

## Current Behavior

- A normal station repair click keeps Valheim's one-item behavior.
- `Left Shift` + station repair click repairs all eligible gear.
- A normal hammer repair click keeps Valheim's one-piece behavior.

## In Development

- Mass repair for buildings and structures is disabled. The previous `Left
  Shift` + hammer repair action could report no damaged pieces while the player
  targeted one.
- Revisit mass building repair only after tests confirm that it detects the
  same targeted piece as a normal hammer repair.
