# Spawning

The Spawning module adjusts spawn opportunities for selected native creatures
while preserving Valheim's normal spawn gates and zone ownership.

## In Development

- Benheim divides the native interval for ordinary Leech spawns by three, so
  eligible Leech spawn opportunities occur three times as often. It changes no
  other native spawn gate, including the biome, time, weather, group,
  population-cap, and world-save checks.
- The change applies only to ordinary Leech spawns in the world. It does not
  change spawns created by events or local creature spawners.
- Valheim runs ordinary world spawning on the client that owns each zone. The
  three-times-as-frequent Leech opportunities are consistent only when every
  active zone owner runs the same Benheim version.
- Focused multiplayer gameplay still needs to prove the adjusted interval as
  zone ownership changes between active clients using the same Benheim version.
