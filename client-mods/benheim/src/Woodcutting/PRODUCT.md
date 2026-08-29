# Woodcutting

The Woodcutting module uses the Wood Cutting skill to reduce repetitive axe
swings without creating bonus drops.

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
- When a qualifying player axe hit destroys a Birch or Oak log, the hit can
  convert that log's native Wood/Finewood composition to Finewood. The total
  item count does not change.
- The Finewood chance is Valheim's native 50% at Wood Cutting skill 25 and
  below. It increases linearly to 100% at Wood Cutting skill 100. It reaches
  about 67% at Wood Cutting skill 50 and 83% at Wood Cutting skill 75.
- Stumps and Birch or Oak logs destroyed without a qualifying player axe hit
  keep their native drops.
