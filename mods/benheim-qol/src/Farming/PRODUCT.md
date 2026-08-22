# Farming

The Farming module reduces repetitive harvesting and planting while preserving
Valheim's normal farming costs and restrictions.

## Current Behavior

- Hold `Left Shift` while harvesting a crop, pickup, or beehive. The mod then
  harvests matching targets within 10 meters.
- Hold `Left Shift` while planting to place a centered 5x5 grid.
- Grid planting preserves Valheim's native rules for resource consumption,
  stamina use, tool durability, plant spacing, cultivated-ground checks,
  creator ownership, placement effects, statistics, skill gain, and rotation.
- Planting previews show which grid positions are valid before placement.
- Farming diagnostics record harvest totals and the reason for each invalid
  planting position.

## In Development

- The next candidate is a centered, deterministic 9x9 planting grid. Compared
  with the accepted centered 5x5 grid, it changes only the grid dimensions.
- The 9x9 candidate must preserve Valheim's native rules for resource
  consumption, stamina use, tool durability, plant spacing, cultivated-ground
  checks, creator ownership, placement effects, statistics, skill gain,
  rotation, and the validity of each grid position in the planting preview.
- The 9x9 planting grid remains an unproven candidate until Ben tests it in
  Valheim.
