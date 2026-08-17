# Player Combat

Player Combat owns how Benheim turns skilled and bold play into rewards that
help the player continue the fight. Narrower modules still own their specific
controls, attacks, meter behavior, and presentation.

## In Development

Player Combat has two reward outputs:

- Frequent skilled or risky actions add adrenaline.
- Exceptional moments and sequences earn named combat states with their own
  bonuses and presentation.

Risk alone gives no reward. A player action must demonstrate skill. The earned
combat state then reflects how bold or dangerous that skilled success was.

Earned combat states use a large activation message, an appropriate
native-style effect, and a visible status while their bonus is active. The first
states are:

- `UNTOUCHABLE` rewards consecutive perfect defenses without taking damage.
  Its damage bonus escalates as the streak grows. Once earned, the bonus remains
  until the player takes damage.
- `CLUTCH` rewards a perfect defense by a critically injured player. It should
  feel like the defense prevented death. It grants strong health regeneration
  rather than instant health.
- `BERSERKER` rewards confirmed kills within a short chain. More kills escalate
  the same chain into `SLAUGHTERHOUSE!`. The state grants damage resistance and
  stamina regeneration so the player can sustain bold aggression.

The exact thresholds, bonus strengths, caps, and presentation remain open
pending gameplay testing. `CLUTCH` must use the same observable rule for perfect
parries and perfect dodges. Benheim must not claim that a dodge prevented lethal
damage unless the game exposes evidence that the dodge prevented lethal damage.

[Adrenaline](../Adrenaline/PRODUCT.md) owns meter behavior and tuning.
[Weapon Rhythm](../WeaponRhythm/PRODUCT.md),
[Archery](../Archery/PRODUCT.md), and later combat mechanics can provide skill
signals. [Combat Feedback](../CombatFeedback/PRODUCT.md) owns local camera
response. [Affinities](../Affinities/PRODUCT.md) owns weapon-specific tradeoffs.
