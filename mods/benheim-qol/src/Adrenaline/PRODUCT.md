# Adrenaline

The Adrenaline module rewards skilled and bold combat in two ways. Frequent
actions build Valheim's adrenaline meter. Exceptional moments and sequences
earn named combat states with their own bonuses and presentation.

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

Doubling every positive adrenaline grant is a proven baseline, not the intended
final adrenaline economy. The next tuning pass will combine three parts:

- A more conservative base that still lets adrenaline matter during normal
  combat.
- Larger rewards for skilled actions than for routine attacks.
- Additional rewards when the player succeeds while taking a meaningful risk.

Routine rewards add adrenaline. Rare achievements create earned combat states
instead of adding more adrenaline. Each state uses a large activation message,
an appropriate native-style effect, and a visible status while its bonus is
active.

The first earned combat states are:

- `UNTOUCHABLE` rewards consecutive perfect defenses without taking damage.
  Its damage bonus escalates as the streak grows. Once earned, the bonus remains
  until the player takes damage.
- `CLUTCH` rewards a perfect defense by a critically injured player. It should
  feel like the defense prevented death. It grants strong health regeneration
  rather than instant health.
- `BERSERKER` rewards confirmed kills within a short chain. More kills escalate
  the same chain into `SLAUGHTERHOUSE!`. The state grants damage resistance and
  stamina regeneration so the player can sustain bold aggression.

The exact thresholds, bonus strengths, caps, durations, and presentation remain
open pending gameplay testing. `CLUTCH` must use one honest rule for perfect
parries and perfect dodges. Benheim must not claim that a dodge prevented lethal
damage unless the game exposes that evidence.

The redesign should tune Valheim's existing adrenaline system instead of
creating a separate combat resource. Native meter capacity, spending,
current-fill behavior, status modifiers, and decay remain the starting point.
