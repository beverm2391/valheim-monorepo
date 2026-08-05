# Repair

The Repair module removes repeated repair clicks.

## Current Behavior

- A normal station repair click keeps Valheim's one-item behavior.
- `Left Shift` + station repair click repairs all eligible gear.
- A normal hammer repair click keeps Valheim's one-piece behavior.

## In Development

- While the hammer is in repair mode, `Left Shift` + repair click uses the aimed
  piece as the center of a 20-meter repair area.
- The aimed piece can be undamaged. The action repairs each damaged building or
  structure that Valheim's normal repair path accepts in that area.
- Each repair keeps Valheim's native eligibility checks and effects, including
  station, ward, ownership, stamina, eitr, durability, and tool costs.
- After successful repairs, a top-left receipt groups results by the localized
  displayed structure type. Each type uses one line, such as `Repaired 4 Wood
  walls`.
- The action shows no receipt when no structure is repaired.
- The implementation has passed the build, full automated client suite, and an
  independent native-path review. Gameplay behavior is not yet proven.
