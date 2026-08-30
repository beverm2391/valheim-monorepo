# Interaction

The Interaction module makes nearby stations and objects less fussy to target
without enabling remote use across a base.

## Current Behavior

- Cauldrons and crafting stations can be used from farther away than Valheim's
  default range.
- When a player opens a chest from extended range, both the player inventory
  and chest inventory appear.
- Benheim changes only the range at which Valheim detects comfort furniture,
  from exactly 10 meters to 20 meters. Comfort furniture in nearby rooms, on
  nearby floors, and in nearby buildings can provide comfort.
- `bh debug comfort` records one Valheim comfort calculation, then stops. It
  does not change the player, furniture, or world. The diagnostic records the
  radius used for that calculation, the shelter and comfort state, and every
  candidate exposed by Valheim's native query. For each candidate, it records
  whether Valheim counted or skipped it and why. It also records a limited
  number of the nearest pieces excluded by the radius. It cannot record pieces
  that Valheim excludes before the native comfort query.

Benheim does not change furniture comfort values or how Valheim resolves
duplicate furniture and furniture groups. It also does not change shelter and
fire requirements, the Rested calculation, persistence, or networking.

## In Development

Benheim doubles only the build-piece placement coverage resolved from native
Workbench and Stonecutter stations. In installed Valheim `0.221.12`, both
level-1 stations have a native 20-meter build radius, so their candidate radius
is 40 meters. Benheim includes native Workbench extension contributions when
it resolves the range, then doubles the total.

This client-only behavior changes only whether the game considers the required
Workbench or Stonecutter in range during piece placement. Each player who wants
this coverage needs a compatible Benheim client. Benheim does not change station
interaction, crafting, repair, upgrade attachment, comfort, Workbench suppression,
enemy spawning, wards, other crafting stations, persistence, networking, or world
data.

The `0.1.76` candidate still needs gameplay proof for:

- the native 20-meter boundary at each level-1 station;
- the extended area beyond 20 meters and through 40 meters;
- the area beyond 40 meters; and
- each station's native crafting, repair, upgrade, and interaction behavior.

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
