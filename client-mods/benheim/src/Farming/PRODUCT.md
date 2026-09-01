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

- The `0.1.77` candidate always produces the same centered 9x9 grid from the same
  planting input.
  Compared with the accepted centered 5x5 grid, it changes only the grid
  dimensions and the stamina cost of successful planting.
- Each successful ordinary or grid plant placement costs 25% of the native
  planting stamina cost that Valheim has already resolved. Skipped, failed, and
  rejected placements cost no stamina.
- The candidate otherwise preserves native resource consumption, tool
  durability, plant spacing, cultivated-ground checks, creator ownership,
  placement effects, statistics, skill gain, rotation, preview validity,
  cultivating and terrain actions, food, all other stamina behavior, crops,
  growth, networking, and saves.
- The candidate adds the native RaspberryBush, BlueberryBush, and
  CloudberryBush to the Cultivator. Planting each bush costs five berries of its
  matching type.
- Berry bushes can be planted only on ordinary ground. They do not require
  cultivated ground or a matching biome. In a 9x9 grid, each bush's native
  collider determines the spacing between bushes.
- Each planted bush uses its native network prefab. It keeps the exact native
  visual, `Pickable` output, 300-minute respawn, `Destructible` behavior, `ZDO`
  persistence, and creator ownership. Players cannot remove naturally spawned
  or planted bushes with the Hammer or Cultivator.
- Benheim adds the `Piece` component, but not the `Plant` component, to each
  native network prefab. It does not create a custom prefab or persistent
  object. Removing the feature leaves the world readable. Planted bushes remain
  native `Pickable` objects.
- Players could not place berry bushes with Benheim `0.1.78` on installed
  Valheim `0.221.12`. During registration, Benheim tried to derive each bush's
  placement footprint from world-space collider bounds. Because the native
  prefab templates were inactive, Unity returned an empty footprint.
- The current source derives each bush's placement footprint from its native
  collider shapes and transforms. It no longer reads world-space bounds during
  registration.
- Live single-player acceptance remains unproven. Testing must confirm:
  - ordinary single-bush placement
  - centered 9x9 grid placement
  - exact berry costs
  - native harvesting and the 300-minute respawn
  - save reloads
- Live multiplayer acceptance remains unproven. Testing must confirm:
  - shared placement and harvesting
  - creator ownership
  - reconnect behavior
- The 9x9 planting grid remains an unproven candidate until Ben tests it in
  Valheim.
