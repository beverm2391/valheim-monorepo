# Weapon Rhythm

Weapon Rhythm extends Valheim's weapon controls and animations. It rewards
players for learning each weapon's timing, charge behavior, cadence, and
spacing. Skilled execution should improve a weapon's result without making
ordinary attacks depend on perfect timing.

## In Development

- Perfect Impact is an experimental approach technique. Make space and jump.
  Connect while descending with at least `7 m/s` of horizontal momentum toward
  the contact point to create a stagger opening for an ordinary follow-up.
- A supported local-player melee attack resolves Perfect Impact at its first
  authored `Character` contact. The player must remain airborne, descend at
  `0.5 m/s` or faster, and move at least `7 m/s` horizontally toward the
  resolved contact point. The rule measures physical velocity instead of input
  or facing.
- The first `Character` contact creates one outcome for the attack. A later
  contact cannot create or reverse that outcome. If the first contact qualifies,
  a later target receives the modifiers only when that contact also meets the
  airborne, descent, and approach conditions.
- A qualified contact multiplies all native damage by `1.15` and its native
  stagger multiplier by `3`. Both values remain playtest tuning.
- A qualified attack shows one local `PERFECT IMPACT` confirmation. Benheim FX
  does not gate this semantic feedback. The attack also requests one Combat
  Feedback shake, which remains separately gated by Combat Shake and Benheim
  FX settings.
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
- The previous attack-start gate failed in gameplay. Automated verification
  passes for the contact-time candidate. Gameplay acceptance remains open.
- Gameplay still must prove all of the following:

  - A first `Character` contact while airborne, descending at least `0.5 m/s`,
    and moving at least `7 m/s` toward the contact point applies `1.15x` native
    damage and `3x` native stagger once.
  - Grounded, rising, and insufficient-approach contacts remain native.
  - `PERFECT IMPACT` is visible once, including when Benheim FX is off.
  - Combat Shake and Benheim FX settings gate only the shake.
