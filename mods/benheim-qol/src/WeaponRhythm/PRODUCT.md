# Weapon Rhythm

Weapon Rhythm extends Valheim's weapon controls and animations. It rewards
players for learning each weapon's timing, charge behavior, cadence, and
spacing. Skilled execution should improve a weapon's result without making
ordinary attacks depend on perfect timing.

## In Development

- Perfect Impact is an experimental approach technique. Make space and jump.
  Connect while descending and moving horizontally toward the contact point at
  `5.5 m/s` or faster to create a stagger opening for an ordinary follow-up.
- A supported local-player melee attack resolves Perfect Impact at its first
  authored `Character` contact. The player must remain airborne, descend at
  `0.5 m/s` or faster, and move at least `5.5 m/s` horizontally toward the
  resolved contact point. The rule measures physical velocity instead of input
  or facing.
- The first `Character` contact creates one outcome for the attack. A later
  contact cannot create or reverse that outcome. If the first contact qualifies,
  a later target receives the modifiers only when that contact also meets the
  airborne, descent, and approach conditions.
- A qualified contact multiplies all native damage by `1.15` and its native
  stagger multiplier by `3`. Both values remain playtest tuning.
- A qualified attack requests one local `PERFECT IMPACT` through Valheim's
  native world text at the struck `Character` and contact point. Benheim FX does
  not gate this semantic feedback. The attack also requests one Combat Feedback
  shake, which remains separately gated by Combat Shake and Benheim FX settings.
- Grounded, rising, near-apex, and insufficient-approach contacts remain native.
  Contacts with destructibles, terrain, and gathering targets also remain
  native.
- The rule supports native primary and secondary horizontal, vertical, and area
  melee attacks. It excludes projectiles, ranged attacks, status and
  damage-over-time effects, enemy attacks, and unrelated damage calls.
- The rule does not change input, stamina, movement, animation, hit geometry,
  landing behavior, networking, or persistence. The attacker still creates the
  native hit. The target owner still decides block, dodge, resistance, armor,
  health, stagger, and death through the native damage path.
- Automated verification records one outcome for each resolved attack.
- The previous attack-start gate failed in gameplay. The first live contact-time
  test observed 193 contacts. None qualified. In the closest clear descending
  attempt, vertical velocity was `-4.93 m/s`, and approach speed was
  `6.744 m/s`. Only the previous `7 m/s` approach threshold rejected this
  attempt. The Perfect Impact candidate lowers the approach threshold from
  `7 m/s` to `5.5 m/s`.
- A later live Lox contact qualified at `-6.955 m/s` vertical speed and
  `7.967 m/s` approach speed. It applied `1.15x` native damage and `3x` native
  stagger. The top-left confirmation was not visible even though its diagnostic
  claimed `feedback=placed`; that presentation path is rejected. The current
  candidate requests the confirmation through native world text instead.
- Gameplay still must prove all of the following:

  - Grounded, rising, and insufficient-approach contacts remain native.
  - Native world text makes `PERFECT IMPACT` visible once at the struck
    character, including when Benheim FX is off.
  - Combat Shake and Benheim FX settings gate only the shake.
