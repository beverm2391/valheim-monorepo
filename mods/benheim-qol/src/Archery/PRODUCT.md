# Archery

The Archery module adds global headshots to ordinary player-fired Bow arrows
while leaving Valheim's native damage ownership and acceptance rules in charge.

## Current Behavior

- Ben confirmed global Bow headshots on a Berserker and a Shaman in `0.1.49`.
- Ben confirmed that `0.1.50` can qualify some Lox headshots.

## In Development

- A player-fired Bow arrow that strikes the identified head-centered collider
  receives a Benheim damage multiplier across that collider's exact volume.
  This collider is centered on the animated Head point, unlike a broad body
  collider that also overlaps that point. Benheim does not apply the smaller
  Head-point tolerance again after the arrow strikes the head-centered
  collider.
- Hits on other colliders keep the existing scale-relative Head-point rule. Its
  tolerance still uses the struck and root collider dimensions at the
  creature's current scale.
- The multiplier is `1.25x` through 20 meters, increases linearly from `1.25x`
  at 20 meters to `1.50x` at 60 meters, and stays capped at `1.50x` beyond 60
  meters.
- Benheim does not add stagger or force a stun. Native unaware and backstab
  damage, resistances, armor, difficulty scaling, and other target-owner damage
  rules still apply normally.
- A hit on any prefab-specific native WeakSpot stays entirely native. Benheim
  does not add its global headshot multiplier or feedback to the same hit.
- `HEADSHOT` text together with a `[diag][Headshots] applied` event confirms
  Benheim's local head qualification. These signals do not claim that the
  target owner accepted the damage or that the target died.
- A critical sound alone does not prove a Benheim headshot. Valheim can play
  its own critical effect for a native WeakSpot or damage to an already
  staggering creature.
- Valheim still sends the modified hit through its normal target-owner damage
  path. Benheim adds no headshot result protocol, marker, retry, timeout, or
  persistent shot state.
- Ordinary arrow adrenaline remains unchanged.
- Automated geometry checks prove that an outer-surface hit on the identified
  head-centered collider qualifies, while a broad overlapping body collider
  keeps the old fallback. This automated proof is not gameplay proof, so the
  exact-volume behavior still needs a focused gameplay retest.

## Open Product Decision

- `SNIPED` kill feedback and headshot-specific adrenaline rewards remain
  deliberately omitted. Decide after the first successful gameplay test
  whether they are valuable enough to justify a custom multiplayer result
  protocol and transient shot lifecycle. Valheim does not return accepted
  damage, authoritative death, full-health state, or the shooter's actual meter
  delta through its native projectile path, so this slice does not claim those
  facts.
