# Interaction

The Interaction module makes nearby stations and objects less fussy to target
without enabling remote use across a base.

## Current Behavior

- Cauldrons and crafting stations can be used from farther away than Valheim's
  default range.
- When a player opens a chest from extended range, both the player inventory
  and chest inventory appear.

## In Development

Players can manually collect native Tar while the collectible is submerged in
a native tar pit. This exception applies only to native Tar and only during
manual interaction. Native interaction range, ownership, pickup requests,
inventory capacity, carry weight, effects, and ordinary failure behavior still
apply.

Submerged Tar does not auto-pick up; its native behavior remains unchanged.
Other submerged items remain stuck. Benheim does not:

- move Tar or change its status hazards;
- drain or mutate the pit;
- change terrain or locations; or
- write new world or character state.

This behavior needs gameplay proof.

Benheim changes only Valheim's comfort-furniture detection range, from exactly
10 meters to 20 meters. Comfort furniture in nearby rooms, on nearby floors,
and in nearby buildings can provide comfort.

Benheim does not change:

- furniture comfort values or how Valheim resolves duplicate furniture and
  furniture groups;
- shelter and fire requirements or Rested calculation; or
- persistence or networking.

This behavior needs gameplay proof.
