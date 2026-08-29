# Woodcutting

The Woodcutting module reduces repetitive axe swings and makes Finewood easier
to obtain without increasing native drop counts.

## Current Behavior

- No woodcutting changes have been gameplay-confirmed yet.

## In Development

- Cleave unlocks at Wood Cutting 25 for local-player axe hits against standing
  trees and fallen logs.
- Cleave chance increases from 30% at Wood Cutting 25 to 85% at Wood Cutting
  100.
- A cleave applies one secondary hit that deals 50% of the original hit's
  damage to the exact standing tree or fallen log that the player hit. It does
  not damage nearby trees, logs, structures, or creatures.
- Cleave uses Valheim's normal destruction behavior and does not add bonus
  drops.
- A successful cleave shows yellow `CLEAVE` combat text at the axe impact,
  using the same feedback style as mining's `AOE` proc.
- Native Birch and Oak logs convert each final ordinary Wood drop to Finewood.
  Their native Finewood drops and all non-Wood drops remain unchanged.
- The conversion keeps each log's native item count unchanged. Valheim still
  spawns every drop through its native path.
- Other logs remain native.
- Standing-tree drops and stumps remain native.
- Native damage-type conversions and unrelated destruction remain native.
- The conversion works when one compatible client attacks a log that another
  compatible client owns.
