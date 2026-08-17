# Player Combat

Player Combat owns how Benheim turns skilled and bold play into rewards that
help the player continue the fight. Narrower modules still own their specific
controls, attacks, meter behavior, and presentation.

## In Development

Player Combat gives two types of reward:

- Frequent skilled or risky actions add adrenaline.
- Exceptional moments and sequences earn named combat states with their own
  bonuses and presentation.

Skill and boldness increase an action's reward independently. Either can
increase the reward. Together, they can give the action its highest reward. Low
health can increase the reward for ordinary melee that already earns
adrenaline. Bold aggression while outnumbered can also increase an action's
reward.

Earned combat states use a large activation message, an appropriate
native-style effect, and a visible status while their bonus is active. The first
states are:

- `UNTOUCHABLE` rewards consecutive perfect defenses without taking damage.
  Its damage bonus escalates as the streak grows. Once earned, the bonus remains
  until the player takes damage.
- `CLUTCH` rewards a perfect defense while the player is at critical health. It
  grants strong health regeneration rather than instant healing. Its first
  version activates when a player at critical health makes a perfect defense.
  It does not claim that the avoided attack would have been lethal.
- `BERSERKER` rewards confirmed kills within a short chain. More kills escalate
  the same chain into `SLAUGHTERHOUSE!`. The state grants damage resistance and
  stamina regeneration so the player can sustain bold aggression.

The exact thresholds, bonus strengths, caps, and presentation remain open
pending gameplay testing. `CLUTCH` must use the same activation rule for perfect
parries and perfect dodges. Exact avoided-damage evidence may refine the rule
later.

[Adrenaline](../Adrenaline/PRODUCT.md) owns meter behavior and tuning.
[Weapon Rhythm](../WeaponRhythm/PRODUCT.md),
[Archery](../Archery/PRODUCT.md), and later combat mechanics can provide skill
signals. [Combat Feedback](../CombatFeedback/PRODUCT.md) owns local camera
response. [Affinities](../Affinities/PRODUCT.md) owns weapon-specific tradeoffs.
