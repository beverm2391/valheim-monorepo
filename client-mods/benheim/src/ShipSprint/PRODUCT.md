# Ship Sprint

Ship Sprint makes long sailing legs faster through Valheim's existing helm and
Run control. While a player controls a native ship, holding Valheim's logical
Run control multiplies the ship's native forward thrust by `3x`. This is a
thrust tuning value, not a promise of `3x` terminal velocity.

The boost applies to forward paddle, half sail, and full sail. Reverse remains
native so docking stays controllable. Releasing Run or leaving the helm stops
the boost. The boost also stops when a disconnect occurs, the world becomes
unavailable, or Benheim shuts down the feature.

Ship Sprint changes no other sailing rule. Ship Sprint does not change native
wind effectiveness, drag, steering, buoyancy, waves, collisions, water impacts,
damage, throttle states, or momentum decay. Ship Sprint adds no stamina cost,
fuel, cooldown, status icon, saved state, or separate progression system.

While the local player controls a ship, a compact readout beside Valheim's ship
controls shows the ship's planar world speed in meters per second. It shows one
decimal place. A subtle `SPRINT` label appears only while the local player has
an active Ship Sprint request. The readout disappears when the player leaves the
helm, the ship or world becomes unavailable, or Benheim shuts down. It uses
native HUD text and adds no custom assets, saved UI state, network state, or
diagnostics.

Every client that can become the ship's physics owner must run a compatible
Benheim build. The physics owner is the client that applies the ship's physics.
The controlling player can differ from the physics owner. Only the current
physics owner applies the boost after validating transient helm input against
Valheim's current controller.

## In Development

The first `3x` thrust candidate and speed readout have executable source and
rules proof. A live test must still confirm the readout's placement and
readability, its planar speed display, its `SPRINT` label, and its disappearance
in every condition named above. A live multiplayer test must also confirm:

- each forward throttle;
- non-owner control;
- owner handoff;
- release;
- helm exit; and
- ordinary reverse behavior.

When a sprint ends, typed diagnostics record the ship type, duration, starting
speed, and peak speed. They do not emit per-frame log events.
