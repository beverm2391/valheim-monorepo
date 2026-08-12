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
- One interaction fills one material type. A selected item chooses its type.
  Without a selected item, Benheim chooses the first available type in the
  Valheim conversion order for that station. It does not mix input types
  during one interaction.
- A locally owned station keeps the existing instant add-one flow for the
  chosen type. Shield Generator and cooking-station batches keep their existing
  flow.
- For a remotely owned station that uses Valheim's shared `Smelter` component,
  Benheim sends one request to the station owner. The owner fills the chosen
  input or fuel type. This includes the Smelter, Charcoal Kiln, Blast Furnace,
  Windmill, Spinning Wheel, and Eitr Refinery.
- The requester removes the chosen materials first. The station owner checks
  its live allowed input and capacity, applies the accepted count without
  replication waits, and returns that count. The requester refunds the
  rejected remainder.
- Benheim keeps transient state only until it receives the owner's result and
  refunds the rejected remainder. Benheim does not retry the request or
  preserve state after a disconnect or crash.
- Show one centered summary with the number of items added.
- Production diagnostics record the requester, owner, requested amount,
  accepted amount, refunded amount, result, and elapsed time.
- The next multiplayer proof must fill an empty remote-owned Windmill, then a
  nearly full remote-owned Windmill. Inventory and station counts must match
  the reported accepted and refunded amounts immediately after each result.
- `0.1.51` adds Shield Generator fuel handling separately, but focused gameplay
  proof is still required.
- In `0.1.48`, diagnostics confirmed ten Stone Oven conversions were multiplied
  by `0.5` and the player Valheim assigned as owner ran the timer. Bread still
  felt long. The next test must report each recipe's native and effective bake
  and done-to-burn seconds so Ben can separate a broken patch from a timing
  preference.
