# Production

The Production module fills routine station inputs without repeated clicks.

## Current Behavior

- Normal station interaction adds one item at a time.
- The `0.1.50` log proves one ordinary Smelter ore case: one `Left Shift`
  interaction filled the input from `0` to `50`. The result does not prove
  every station or ownership case.
- In `0.1.50`, Ben's gameplay logs prove two complete Windmill fills. One
  `Left Shift` interaction filled an empty Windmill from `0` to `50` by adding
  `50` Barley. Another filled a Windmill from `48` to its `50`-item capacity
  by adding `2` Barley. The diagnostics used the shared Smelter component
  label, not a Windmill-specific label.
- An earlier Windmill interaction added only one Barley. The result remains
  intermittent and unreproduced. The successful partial-fill test rules out a
  partially filled queue as a sufficient cause. A first-use or ownership
  warmup effect may explain it, but that remains only a hypothesis.

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
- `0.1.51` has no proven Windmill-specific fix. Its Production diagnostics
  record the station, input, owner, attempted additions, confirmed station
  updates, result, and elapsed time.
- The next focused proof must use those diagnostics on the first Windmill
  `Left Shift` fill after joining a world or on a newly placed empty Windmill.
  First-use or ownership warmup remains only a hypothesis.
- `0.1.51` adds Shield Generator fuel handling separately, but focused gameplay
  proof is still required.
- In `0.1.48`, diagnostics confirmed ten Stone Oven conversions were multiplied
  by `0.5` and the player Valheim assigned as owner ran the timer. Bread still
  felt long. The next test must report each recipe's native and effective bake
  and done-to-burn seconds so Ben can separate a broken patch from a timing
  preference.
