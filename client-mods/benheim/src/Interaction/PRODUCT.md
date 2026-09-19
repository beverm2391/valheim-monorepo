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
- `bhrun comfort` records one Valheim comfort calculation, then stops. It
  does not change the player, furniture, or world. The diagnostic records the
  radius used for that calculation, the shelter and comfort state, and every
  candidate exposed by Valheim's native query. For each candidate, it records
  whether Valheim counted or skipped it and why. It also records a limited
  number of the nearest pieces excluded by the radius. It cannot record pieces
  that Valheim excludes before the native comfort query.
Benheim does not change furniture comfort values or how Valheim resolves
duplicate furniture and furniture groups. It also does not change shelter and
fire requirements, the Rested calculation, persistence, or networking.

Benheim extends only feast targeting and eating from Valheim's native 2-meter
range to Benheim's existing 8-meter interaction range. Food eligibility,
remaining portions, owner-authoritative requests, effects, and all other feast
behavior remain native. Ben accepted the extended Feast targeting in live play.

All dropped items use Valheim's native buoyancy behavior in water and tar.
Items that already float keep their native behavior. Items that do not reuse
the native Wood buoyancy profile so they rise and settle at the liquid surface
without launching upward.

The behavior changes no item identity, count, ownership, pickup rule, or saved
item and world data. Manual pickup range, inventory capacity, carry weight,
ownership, and the player's auto-pickup setting remain native. Benheim does
not move or mutate the liquid, terrain, or location.

## In Development

Benheim doubles Valheim's native automatic pickup radius for ordinary dropped
items, including items floating in water or tar. This applies only when the
player has auto-pickup enabled. Manual pickup range stays native, as do item
eligibility, ownership, inventory capacity, carry weight, and failure behavior.
The change does not pull or relocate items before native pickup, alter their
identity or count, or persist new world or character data.

The `bhrun comfort` command prints a readable summary in the console.
The summary includes the calculated comfort and **Counted**, **Ignored**, and
**Just outside range** sections. The command still records the complete
diagnostic in its structured form and writes the evidence to the log. The
summary needs live console proof.

Benheim gives native Workbench and Stonecutter stations a coherent Hammer work
zone at twice their native range. In installed Valheim `0.221.12`, both level-1
stations have a native 20-meter build radius, so their candidate Hammer work
zone is 40 meters. Benheim includes native Workbench extension contributions
when it resolves that range, then doubles the total.

Inside the extended zone, a compatible client can place pieces that require the
station, repair eligible pieces, and dismantle pieces. Valheim's dashed station
boundary and Hammer station-range state show the same extended zone. Beyond the
extended boundary, each action and indicator returns to native out-of-range
behavior.

This client-only behavior does not extend station interaction or crafting,
upgrade-piece attachment, comfort, Workbench suppression, enemy spawning,
wards, other crafting stations, persistence, networking, or world data. Each
player who wants the extended Hammer work zone needs a compatible Benheim
client.

Gameplay proof must establish that each station's boundary and Hammer actions
agree throughout the native, extended, and out-of-range areas while every
excluded station behavior remains native.
