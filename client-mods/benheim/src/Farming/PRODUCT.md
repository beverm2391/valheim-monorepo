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

- Compared with the accepted centered 5x5 grid, the candidate adds selectable
  odd grid sizes and changes the stamina cost of successful planting.
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
  cultivated ground or a matching biome. For the next playtest, berry-bush grid
  spacing is twice the spacing derived from each bush's native collider
  footprint. This gives bushes more room.
  The preview and placement use the same spacing at every grid size. Ordinary
  crop spacing and placement restrictions stay unchanged. Existing bushes do
  not move.
- Each newly planted bush uses its native network prefab and starts empty.
  Before its first yield, Benheim deterministically selects a new wait from
  4,000 to 5,000 seconds. After every harvest, Benheim deterministically
  selects another wait in that range for any planted or naturally spawned
  Raspberry, Blueberry, or Cloudberry bush. These bushes otherwise keep the
  exact native visual, `Pickable` output, `Destructible` behavior, and `ZDO`
  persistence. Every other `Pickable` object keeps its native timing.
- A player may use the Hammer to remove a player-planted berry bush when
  Valheim's native access and ward rules permit removal. Removal returns exactly
  five berries matching the bush type. Benheim uses the creator marker only to
  identify the bush as player-planted. The original planter receives no
  additional removal authority. Naturally spawned bushes cannot be removed with
  the Hammer. The Cultivator cannot remove planted or naturally spawned berry
  bushes.
- Removal uses Valheim's normal path for non-structural `Piece` objects.
  Benheim does not add a custom removal protocol. A repeated removal after
  native destruction cannot refund twice. If two authorized peers remove the
  same bush before that destruction replicates between them, Valheim may grant
  both native refunds. Benheim
  accepts this narrow simultaneous race rather than adding a separate network
  authority system for a five-berry refund.
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
- While the local player's Cultivator piece picker is open and `Left Shift` is
  held, pressing `1`, `3`, `5`, `7`, or `9` selects a centered grid of that
  size. Benheim confirms the selection immediately. The selected size controls
  the existing `Left Shift` mass-plant preview and placement.
- Each time the local player opens the Cultivator picker, the grid selection
  resets to 5x5. Benheim does not carry a selection into the next picker
  session.
- Benheim does not intercept a number key unless the Cultivator picker is open,
  `Left Shift` is held, and the key is `1`, `3`, `5`, `7`, or `9`. Every other
  number-key input keeps its native behavior.
- Live `0.1.80` proved ordinary Raspberry placement. It also showed that newly
  planted Raspberry bushes did not start empty and that Cultivator grid-size
  selection intercepted keys outside the required `Left Shift` combinations.
- Live `0.1.81` testing proved one case: Ben removed one player-planted
  Raspberry bush with the Hammer and received exactly five Raspberries. All
  other removal cases, fixes, and remaining single-player behavior remain
  unproven. Testing must confirm:
  - ordinary Blueberry and Cloudberry placement
  - centered 9x9 grid placement
  - exact berry costs
  - one newly planted bush of each type starts empty and later produces berries
  - native harvesting empties a planted bush, which later produces another
    yield
  - focused timing proof confirms that Benheim deterministically selects a new
    wait from 4,000 to 5,000 seconds before each planted bush's first yield and
    after every harvest of a planted or naturally spawned bush
  - one naturally spawned bush of each type later produces berries after
    harvest
  - unrelated `Pickable` objects retain their native timing
  - Hammer removal returns exactly five matching berries for one player-planted
    Blueberry bush and one player-planted Cloudberry bush
  - one naturally spawned bush of each type cannot be removed with the Hammer
  - the Cultivator cannot remove a planted or naturally spawned bush of any type
  - berry-bush state persists after a save reload
  - each Cultivator-picker session starts with a 5x5 selection and does not
    restore the prior picker session's selection
  - `Left Shift` plus each odd number key produces immediate selection
    confirmation
  - after each odd size is selected, the existing `Left Shift` mass-plant
    preview and placement use the corresponding centered dimensions
  - plain number keys and every other number-key combination keep native
    behavior
- Live multiplayer acceptance remains unproven. Testing must confirm:
  - shared placement and harvesting
  - creator ownership
  - a peer who did not plant the bush can remove it when native access and ward
    rules permit removal, while those rules still block unauthorized removal
  - reconnect behavior
- The corrected selectable planting grids remain unproven until Ben tests them
  in Valheim.
- In the live test of installed `0.1.81`, holding `Left Shift` and pressing an
  odd-number key did not select a grid. No top-left confirmation or typed
  `Farming.plant_grid_selected` event appeared. Ben reported that selection
  still failed in `0.1.83`. The next candidate must restore the 5x5 default.
  Grid selection remains unaccepted until a corrected build passes live review.
