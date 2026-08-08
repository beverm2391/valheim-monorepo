# Archery

The Archery module adds global headshots to ordinary player-fired Bow arrows
while leaving Valheim's native damage ownership and acceptance rules in charge.

## Current Behavior

None yet. The first headshot slice still needs gameplay proof.

## In Development

- A player-fired Bow arrow that lands within the creature's scale-relative head
  region receives a Benheim damage multiplier. Qualification uses the arrow's
  real impact point, the creature's animated Head bone, and a tolerance derived
  from the struck and root collider dimensions at the creature's current scale.
- The multiplier is `1.25x` through 20 meters, increases linearly from `1.25x`
  at 20 meters to `1.50x` at 60 meters, and stays capped at `1.50x` beyond 60
  meters.
- Benheim does not add stagger or force a stun. Native unaware and backstab
  damage, resistances, armor, difficulty scaling, and other target-owner damage
  rules still apply normally.
- A hit on any prefab-specific native WeakSpot stays entirely native. Benheim
  does not add its global headshot multiplier or feedback to the same hit.
- `HEADSHOT · 37m · ×1.36`-style text and a restrained native critical-hit
  effect appear immediately at collision time. This confirms Benheim's local
  head qualification, not that the target owner accepted the damage or that the
  target died.
- Valheim still sends the modified hit through its normal target-owner damage
  path. Benheim adds no headshot result protocol, marker, retry, timeout, or
  persistent shot state.
- Ordinary arrow adrenaline remains unchanged.
- Ben reported that attempted Bow headshots on a Lox produced no feedback. The
  `0.1.48` log contained no Headshots diagnostic event. The next candidate must
  log the qualification decision and accept real Lox head hits.

## Open Product Decision

- `SNIPED` kill feedback and headshot-specific adrenaline rewards remain
  deliberately omitted. Decide after the first successful gameplay test
  whether they are valuable enough to justify a custom multiplayer result
  protocol and transient shot lifecycle. Valheim does not return accepted
  damage, authoritative death, full-health state, or the shooter's actual meter
  delta through its native projectile path, so this slice does not claim those
  facts.
