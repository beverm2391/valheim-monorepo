# Production

The Production module fills routine station inputs without repeated clicks.

## Current Behavior

- Normal station interaction adds one item at a time.
- The `0.1.50` log proves one ordinary Smelter ore case: one `Left Shift`
  interaction filled the input from `0` to `50`. The result does not prove
  every station or ownership case.
- Ben observed a different `0.1.50` result at the Windmill: one `Left Shift`
  interaction added only one Barley instead of filling the input. That session
  contains no Production diagnostics, so it does not establish why the action
  added only one Barley.

## In Development

- The Stone Oven takes half as long to bake each recipe. Its done-to-burn
  window is also halved.
- Other cooking stations and fuel use remain unchanged.
- In an all-modded multiplayer session, whichever player Valheim makes the
  Stone Oven owner must run the same shortened native timer.
- Hold `Left Shift` to batch-fill inputs and fuel for the Smelter, Charcoal
  Kiln, Blast Furnace, Windmill, Spinning Wheel, and Eitr Refinery. The same
  action fills Shield Generator fuel and cooking-station food and fuel to
  available capacity.
- During batch fill, a selected item limits the fill to that item type. Without
  a selected item, batch fill follows Valheim's compatible-item order.
- Invoke Valheim's normal add-one action once per item. Each invocation
  preserves Valheim's capacity checks, inventory changes, effects, skill
  behavior, and network synchronization.
- Stop when the station is full, the player runs out of compatible items,
  Valheim rejects an addition, or synchronization exceeds its timeout.
- Show one centered summary with the number of items added.
- `0.1.51` has no proven Windmill-specific fix. It adds Production diagnostics
  that record the station, input, owner, attempted additions, confirmed station
  updates, result, and elapsed time.
- Windmill behavior remains unproven when the local player owns it and when
  another player owns it. Acceptance requires one `Left Shift` interaction to
  add Barley until the Windmill reaches native capacity or the player's
  inventory has no compatible Barley left. Normal interaction must still add
  one item.
- `0.1.51` adds Shield Generator fuel handling separately, but focused gameplay
  proof is still required.
- In `0.1.48`, diagnostics confirmed ten Stone Oven conversions were multiplied
  by `0.5` and the player Valheim assigned as owner ran the timer. Bread still
  felt long. The next test must report each recipe's native and effective bake
  and done-to-burn seconds so Ben can separate a broken patch from a timing
  preference.
