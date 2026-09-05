# Woodcutting

The Woodcutting module reduces repetitive axe swings and makes Finewood easier
to obtain without increasing native drop counts.

## Current Behavior

- Each final ordinary Wood drop from a native Birch or Oak log becomes
  Finewood. Native Finewood drops and all non-Wood drops from those logs remain
  unchanged.
- The conversion keeps each log's native item count unchanged. Valheim still
  creates every drop through its normal drop process.
- Drops from logs other than native Birch and Oak, standing trees, and stumps
  remain unchanged.
- Valheim's native damage-type conversions and unrelated destruction remain
  unchanged.
- A compatible client converts the drops when it attacks a log owned by
  another compatible client.

## In Development

- Each final ordinary Wood drop from a native Pine log becomes Finewood.
  Native Core Wood drops remain unchanged. The conversion keeps each Pine
  log's native item count unchanged.
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
