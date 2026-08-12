# Weapon Rhythm

Weapon Rhythm extends Valheim's weapon controls and animations. It rewards
players for learning each weapon's timing, charge behavior, cadence, and
spacing. Skilled execution should improve a weapon's result without making
ordinary attacks depend on perfect timing.

## In Development

- When a native melee hit from the local player connects with a `Character`
  while the player is airborne, the hit deals `1.15x` damage and uses `2x` its
  native stagger multiplier. These values are a conservative first-playtest
  tuning, not accepted feel.
- The rule applies to native primary and secondary horizontal, vertical, and
  area melee attacks. It does not apply to projectiles, ranged attacks, status
  or damage-over-time effects, enemy attacks, destructible or terrain hits, or
  gathering targets.
- The hit checks airborne state when native melee geometry finds the target. It
  does not create a jump-attack state or change input, stamina, movement,
  animation, hit geometry, or landing behavior.
- Valheim's attacker still creates the native hit. The target owner still
  decides block, dodge, resistance, armor, health, stagger, and death through
  the ordinary damage path.
- Diagnostics record each qualified local hit and grounded local skip. They do
  not log per frame.
