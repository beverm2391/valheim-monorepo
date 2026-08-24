# Farming

The Farming module reduces repetitive harvesting and planting while preserving
Valheim's normal farming restrictions.

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

- The `0.1.70` candidate is a centered, deterministic 9x9 planting grid.
  Compared with the accepted centered 5x5 grid, it changes only the grid
  dimensions and the stamina cost of successful planting.
- Each successful plant placement costs 50% of Valheim's resolved native
  planting stamina cost. The same proportional cost applies to every successful
  placement in the mass-planting grid; skipped and failed positions are not
  charged.
- The candidate otherwise preserves native resource consumption, tool
  durability, plant spacing, cultivated-ground checks, creator ownership,
  placement effects, statistics, skill gain, rotation, preview validity,
  cultivating and terrain actions, food, all other stamina behavior, crops,
  growth, networking, and saves.
- The 9x9 planting grid remains an unproven candidate until Ben tests it in
  Valheim.
