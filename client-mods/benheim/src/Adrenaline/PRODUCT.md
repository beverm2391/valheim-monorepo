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
- Valheim still applies its normal adrenaline rules once. These include its
  rate, how the current meter fill affects each grant, status modifiers, meter
  cap, and full-meter behavior.
- The normal 10-second delay and subsequent decay remain unchanged.
- Perfect-parry and perfect-dodge feedback must show the amount the doubled
  native grant actually adds to the meter.
- `0.1.48` gameplay diagnostics confirmed doubled positive grants, actual
  perfect-parry feedback, and the unchanged 10-second delay.

## In Development

Doubling every positive adrenaline grant is a proven baseline, not the intended
final adrenaline economy. The next tuning pass will combine three parts:

- A more conservative baseline for ordinary adrenaline gains that still lets
  adrenaline matter during normal combat.
- Larger rewards for skilled actions than for routine attacks.
- Additional rewards when the player succeeds while taking a meaningful risk.

The [Player Combat product](../PlayerCombat/PRODUCT.md) owns which skilled and
risky actions receive those rewards. It also owns earned combat states, which
are separate from the adrenaline meter.

The redesign should tune Valheim's existing adrenaline system instead of
creating a separate combat resource. Native meter capacity, spending,
current-fill behavior, status modifiers, and decay remain the starting point.
