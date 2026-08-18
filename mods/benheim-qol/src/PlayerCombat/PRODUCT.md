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

Earned combat states use local above-player activation text, an appropriate
native-style effect, and a visible status while their bonus is active. Ben
approved the candidates below for experimentation. None has passed gameplay
testing, including its trigger, effects, tuning, or presentation.

### CLUTCH

`CLUTCH` activates when a confirmed perfect parry or perfect dodge occurs while
the player has less than 30 health. It does not claim that the avoided attack
would have been lethal.

The state recovers 60 health over six seconds at 10 health per second. Native
healing caps the result at maximum health. Another confirmed perfect parry or
perfect dodge while the player has less than 30 health refreshes the same
six-second effect. It does not add another effect or icon, and it does not replay
entry presentation. Accepted damage does not cancel the state.

On entry, CLUTCH adds `CLUTCH!` to the originating defense's local blue Bonus
text and plays Valheim's native adrenaline-charm activation cue. `CLUTCH` uses
the Lingering Healing Mead icon for its native status. Death, switching
characters or worlds, and ending the current Benheim session clear the state.

### UNTOUCHABLE

Every confirmed perfect parry or perfect dodge increments one shared streak.
The two defense types can mix, and a defense that activates CLUTCH also counts.
The streak has no timer or encounter reset. It remains through portals,
sailing, sleep, downtime, and separate encounters while the same player is
loaded in the same world.

- Tier I starts at five defenses and adds 10% to all outgoing player damage.
- Tier II starts at eight defenses and replaces Tier I with a 20% bonus.
- Tier III starts at twelve defenses and replaces Tier II with a 30% bonus.

Any accepted health loss resets the streak and quietly removes the active tier.
This includes direct attacks, damage over time, fire, falls, and environmental
damage. A contact that causes no health loss does not reset it. The state has no
decay or cooldown. Death, switching characters or worlds, and ending the
current Benheim session reset the streak and remove the active tier.

Each new tier adds `UNTOUCHABLE!`, `UNTOUCHABLE II!`, or `UNTOUCHABLE III!` to
the originating defense's local blue Bonus text. It also plays the native
adrenaline-charm activation cue. Tier replacement keeps one modifier and one
Wolf Sight status icon. The native indefinite status shows no countdown.

### BERSERKER / SLAUGHTERHOUSE

`BERSERKER` and `SLAUGHTERHOUSE` are two tiers of one earned state that rewards
a rolling chain of server-confirmed kills. [Benheim Server
Support](../../../../server-mods/benheim-server-support/PRODUCT.md) owns kill
qualification, thresholds, and the rolling window.

Tier I, `BERSERKER`, applies native Slightly Resistant physical protection to
blunt, slash, and pierce damage only. It reduces those damage types by 25% and
adds 50% stamina regeneration.

Tier II replaces Tier I and presents `SLAUGHTERHOUSE!`. It applies native
Resistant physical protection to the same three damage types, reducing them by
50%, and adds 100% stamina regeneration. Neither tier changes elemental,
poison, or spirit resistance. Neither tier changes outgoing damage.

Entry and escalation use the same local Bonus text and native charm cue as the
other earned states. A confirmed kill that does not advance the tier refreshes
the one active effect and countdown without replaying the local Bonus text or
charm cue. Both tiers reuse the Crystal Heart status icon. Expiration and
removal are quiet.

Benheim waits up to five seconds for a matching Kill Attribution V2 capability
from the server. If none arrives, BERSERKER/SLAUGHTERHOUSE remains inactive.
Benheim shows one center-screen warning per session. It keeps the warning in the
Controls panel's Warnings block until the matching capability arrives.

One confirmed defense can produce an adrenaline message and multiple
earned-state titles. Benheim combines them into one local Bonus text in the
order that the defense earns them. It also plays at most one charm cue for that
defense. This rule does not combine unrelated actions or callbacks.

None of the three states adds extra adrenaline. Existing perfect-defense
adrenaline behavior remains unchanged. Exact avoided-damage evidence may refine
CLUTCH later.

[Adrenaline](../Adrenaline/PRODUCT.md) owns meter behavior and tuning.
[Weapon Rhythm](../WeaponRhythm/PRODUCT.md),
[Archery](../Archery/PRODUCT.md), and later combat mechanics can provide skill
signals. [Combat Feedback](../CombatFeedback/PRODUCT.md) owns local camera
response. [Affinities](../Affinities/PRODUCT.md) owns weapon-specific tradeoffs.
