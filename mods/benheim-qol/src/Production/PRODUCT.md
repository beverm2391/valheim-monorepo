# Production

The Production module fills routine station inputs without repeated clicks.

## Current Behavior

- Normal station interaction adds one item at a time.

## In Development

- The Stone Oven takes half as long to bake each recipe. Its done-to-burn
  window is also halved.
- Other cooking stations and fuel use remain unchanged.
- In an all-modded multiplayer session, whichever player Valheim makes the
  Stone Oven owner must run the same shortened native timer.
- Hold `Left Shift` while interacting with a production-station input or fuel
  switch to add as much as its capacity and the player's inventory permit.
- Hold `Left Shift` while adding food or fuel to a cooking station to fill its
  available capacity.
- During batch fill, a selected item limits the fill to that item type. Without
  a selected item, batch fill follows Valheim's compatible-item order.
- Invoke Valheim's normal add-one action once per item. Each invocation
  preserves Valheim's capacity checks, inventory changes, effects, skill
  behavior, and network synchronization.
- Stop when the station is full, the player runs out of compatible items,
  Valheim rejects an addition, or synchronization exceeds its timeout.
- Show one centered summary with the number of items added.
- In `0.1.48`, diagnostics confirmed ten Stone Oven conversions were multiplied
  by `0.5` and the player Valheim assigned as owner ran the timer. Bread still
  felt long. The next test must report each recipe's native and effective bake
  and done-to-burn seconds so Ben can separate a broken patch from a timing
  preference.
