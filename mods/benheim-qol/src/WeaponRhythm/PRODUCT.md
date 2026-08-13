# Weapon Rhythm

Weapon Rhythm extends Valheim's weapon controls and animations. It rewards
players for learning each weapon's timing, charge behavior, cadence, and
spacing. Skilled execution should improve a weapon's result without making
ordinary attacks depend on perfect timing.

## In Development

- Perfect Impact is an experimental approach technique: make space, carry
  sprint-level momentum into a jump, and connect while descending for a
  stronger native hit that may create a stagger opening for an ordinary
  follow-up.
- A native melee hit from the local player qualifies only when it connects with
  a `Character` while the player is off the ground, descending at or below
  `-0.5 m/s`, and moving at least `7 m/s` horizontally toward the contact point.
  Both physical thresholds are experimental. They reject rising, near-apex,
  in-place, sideways, backward, and ordinary walk- or jog-jump contacts.
- A qualified hit deals `1.15x` all native damage and uses `3x` its native
  stagger multiplier. The stronger stagger is meant to create an opening after
  a committed approach. Both values are playtest tuning, not accepted balance.
- Every qualified outcome must show `PERFECT IMPACT` in Benheim's local feedback lane.
  It also requests one restrained Combat Feedback shake when Combat Shake is
  enabled. A multi-target or area outcome still presents one confirmation and
  one shake, not one per target. The semantic confirmation must remain visible
  when Benheim FX is off.
- Ben's `0.1.59` logs proved that Perfect Impact qualified, changed the outgoing
  hit, and requested shake, but its text was not visible. The `0.1.60` candidate
  updates the shared transient lane and reports `unavailable`,
  `created_not_placed`, or `placed`. Gameplay still must prove that the text is
  visible.
- The rule applies to native primary and secondary horizontal, vertical, and
  area melee attacks. It does not apply to projectiles, ranged attacks, status
  or damage-over-time effects, enemy attacks, destructible or terrain hits, or
  gathering targets.
- The hit checks native ground state and physical velocity when native melee
  geometry finds the target. It projects horizontal velocity toward the actual
  contact point rather than checking sprint input, total speed, or facing.
  Every nonqualifying contact remains a completely native hit. The rule does
  not create a jump-attack state or change input, stamina, movement, animation,
  hit geometry, or landing behavior.
- Valheim's attacker still creates the native hit. The target owner still
  decides block, dodge, resistance, armor, health, stagger, and death through
  the ordinary damage path.
- Diagnostics record each qualified local hit and each `grounded`,
  `rising_or_apex`, or `insufficient_approach` local skip. They include vertical
  speed, toward-target speed, both thresholds, both multipliers, and the actual
  text-lane outcome. They do not log per frame.
