# Adrenaline

The Adrenaline module makes adrenaline gains more rewarding, shows feedback for
successful perfect defenses, and shows decay timing on Valheim's meter.

## Current Behavior

- Perfect parries and perfect dodges show yellow `+N` feedback above the player.
- Valheim's adrenaline meter shows a countdown before and during decay.
- The decay countdown appears below the meter, remains readable during
  gameplay, and disappears at zero.
- Ordinary blocks and rolls show no adrenaline feedback. Valheim's full-meter
  effect remains unchanged.
- Every positive adrenaline grant is doubled. This includes ordinary hits,
  perfect parries and dodges, staggering an enemy, and Guardian Power.
- Negative adrenaline changes are not doubled.
- Valheim still applies its normal adrenaline rate, current-fill curve, status
  modifiers, meter cap, and full-meter behavior once.
- The normal 10-second delay and subsequent decay remain unchanged.
- Perfect-parry and perfect-dodge feedback must show the amount the doubled
  native grant actually adds to the meter.
- `0.1.48` gameplay diagnostics confirmed doubled positive grants, actual
  perfect-parry feedback, and the unchanged 10-second delay.

## In Development

No adrenaline changes are currently in development.
