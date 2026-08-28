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
fuel, cooldown, status icon, custom UI, saved state, or separate progression
system.

Every client that can become the ship's physics owner must run a compatible
Benheim build. The physics owner is the client that applies the ship's physics.
The controlling player can differ from the physics owner. Only the current
physics owner applies the boost after validating transient helm input against
Valheim's current controller.

## In Development

The first `3x` thrust candidate has executable source and rules proof. A live
multiplayer test must still confirm:

- each forward throttle;
- non-owner control;
- owner handoff;
- release;
- helm exit; and
- ordinary reverse behavior.

When a sprint ends, typed diagnostics record the ship type, duration, starting
speed, and peak speed. They do not emit per-frame log events.
