# Spawning

The Spawning module adjusts ordinary base-world spawn opportunities for
selected native creatures. It preserves every Valheim spawn gate and respects
Valheim's existing zone ownership.

## In Development

- The candidate changes ordinary base-world Leech opportunities from 3x to 5x
  the native rate by dividing Valheim's normal spawn interval by five. It
  preserves every other native spawn gate, including checks for biome, time,
  weather, group, population cap, and world save.
- The change applies only to ordinary base-world Leech spawns. It does not
  affect Leech spawns from events or local creature spawners.
- Each successful adjusted ordinary base-world Leech spawn emits exactly one
  typed event. The event includes source, prefab, and multiplier fields for the
  base-world source, Leech prefab, and 5x opportunity multiplier. Rejected
  ordinary base-world Leech attempts emit no event.
- Installed Valheim `0.221.12` authors this rule with a loaded-population cap of
  `10`, a `200`-second native interval, `50%` chance per opportunity, group size
  `1`, and `5`-meter same-prefab spacing. Benheim currently changes only the
  interval to `40` seconds. Live testing found the resulting swamp population
  sparse, but successful-spawn events alone do not prove that the native cap is
  the bottleneck. Do not change the cap until enough normal-play evidence from
  the Developer Diagnostics `spawns` probe shows whether the population reaches
  or leaves the cap under the current configuration. That evidence must include
  the effective configuration, loaded population, saturation, and cap
  transitions.
- Valheim performs ordinary base-world spawning on the client that owns each
  zone. The adjusted 5x Leech opportunity rate stays consistent only when all
  active zone owners use mutually compatible Benheim versions for the Spawning
  module.
- Multiplayer testing must still prove the adjusted interval when zone
  ownership moves from one active client to another.
