# Weapon Rhythm

Weapon Rhythm extends Valheim's weapon controls and animations. It rewards
players for learning each weapon's timing, charge behavior, cadence, and
spacing. Skilled execution should improve a weapon's result without making
ordinary attacks depend on perfect timing.

## In Development

- Perfect Impact is an experimental approach technique: make space, start an
  airborne melee swing with at least `7 m/s` of physical forward momentum, and
  connect while descending for a stronger native hit that may create a stagger
  opening for an ordinary follow-up.
- A native horizontal, vertical, or area melee swing arms only when the local
  player starts it while airborne with at least `7 m/s` of physical forward
  momentum. The start gate rejects in-place, sideways, backward, and ordinary
  walk- or jog-jump attacks without reading sprint input.
- That same swing must then hit a `Character` while the player remains airborne
  and descends at or below `-0.5 m/s`. If the player lands, is rising, or is near
  the apex, the swing remains native. Contacts with destructibles, terrain, or
  gathering targets also remain native. Both physical thresholds remain
  experimental.
- A qualified hit deals `1.15x` all native damage and uses `3x` its native
  stagger multiplier. The stronger stagger is meant to create an opening after
  a committed approach. Both values are playtest tuning, not accepted balance.
- Every qualified swing must show `PERFECT IMPACT` in Benheim's local feedback
  lane. It also requests one restrained Combat Feedback shake when Combat Shake
  is enabled. A multi-target or area swing presents one confirmation and one
  shake for that swing, not one per target. The semantic confirmation must
  remain visible when Benheim FX is off.
- Ben's `0.1.59` logs proved that an earlier Perfect Impact rule qualified,
  changed the outgoing hit, and requested shake, but its text was not visible.
  The `0.1.60` candidate updates the shared transient lane and reports
  `unavailable`, `created_not_placed`, or `placed`. Gameplay still must prove
  that the text is visible.
- Ben's `0.1.61` attempts repeatedly had strong descent but only `0–4 m/s` of
  contact-time approach speed at the authored hit event, so no attempt applied
  Perfect Impact. The contact-time approach measurement failed to capture the
  intended approach momentum in those attempts. Gameplay has not yet proved the
  new attack-start gate.
- The rule applies to native primary and secondary horizontal, vertical, and
  area melee attacks. It does not apply to projectiles, ranged attacks, status
  or damage-over-time effects, enemy attacks, destructible or terrain hits, or
  gathering targets.
- The rule does not change input, stamina, movement, animation, hit geometry,
  landing behavior, networking, or persistence. Every nonqualifying contact
  remains native.
- Valheim's attacker still creates the native hit. The target owner still
  decides block, dodge, resistance, armor, health, stagger, and death through
  the ordinary damage path.
- Structured diagnostics correlate each airborne attack start with its first
  `Character` contact or the end of a swing that made no such contact. They
  record whether forward momentum armed the swing or failed the start gate, the
  final decision to apply or skip Perfect Impact, the forward and vertical
  speed measurements with their thresholds, both multipliers, and the actual
  text-lane outcome. They do not log grounded ordinary swings or every frame.
