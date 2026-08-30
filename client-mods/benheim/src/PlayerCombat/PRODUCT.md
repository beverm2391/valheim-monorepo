# Player Combat

Player Combat owns how Benheim turns skilled and bold play into rewards that
help the player continue the fight. Narrower modules still own their specific
controls, attacks, meter behavior, and presentation.

## Current Behavior

In `0.1.66`, Ben reached UNTOUCHABLE Tier I after five confirmed perfect
defenses without losing health. He reached Tier II after eight and Tier III
after twelve. Tier II and Tier III each replaced the previous tier. Every
active tier remained visible in Valheim's native status bar. Ben accepted all
three transitions and the status presentation. Native parry chip damage reset
the streak and removed the active tier.

This acceptance covers the tier thresholds, tier replacement, reset from the
tested native parry chip damage, and native status presentation. It does not
prove the outgoing damage bonuses, the hostile-kill progression, or
every other reset source.

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
approved the candidates below for experimentation. Ben accepted BERSERKER's
title and native status-bar icon. Typed events prove that its native effect
applied, appeared in the HUD, refreshed, and expired. Its configured bonuses
have not been measured manually. The remaining triggers, tuning, and
presentation still need gameplay proof in [Product
Review](../../../../PRODUCT_REVIEW.md).

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

Each native attack or dodge can produce at most one confirmed perfect defense,
even if Valheim reports the same result more than once. Each confirmed perfect
parry or perfect dodge adds one point to a shared streak. Each server-confirmed
qualifying hostile kill also adds one point. Defenses and kills can mix, and a
defense that activates CLUTCH also counts. The streak has no timer or encounter
reset. It remains through portals, sailing, sleep, downtime, and separate
encounters while the same player is loaded in the same world.

- Tier I starts at five streak points and adds 10% to all outgoing player damage.
- Tier II starts at eight streak points and replaces Tier I with a 20% bonus.
- Tier III starts at twelve streak points and replaces Tier II with a 30% bonus.

Accepted health loss from direct attacks, damage over time, fire, falls, and
environmental effects resets the streak and quietly removes the active tier. An
intentional health cost does the same. Passive maximum-health normalization
from food expiry does not reset the streak. A contact that causes no health loss
does not reset it. The state has no decay or cooldown. Death, switching
characters or worlds, and ending the current Benheim session reset the streak
and remove the active tier.

Each new tier shows `UNTOUCHABLE!`, `UNTOUCHABLE II!`, or `UNTOUCHABLE III!` in
local blue Bonus text. When a defense triggers a tier, the tier title appears
in the originating defense's Bonus text. Each new tier also plays the native
adrenaline-charm activation cue. Tier replacement keeps one modifier and one
Wolf Sight status icon. The native indefinite status shows no countdown.
Benheim treats a tier as active only when Valheim applies the tier's native
effect and includes that effect in the native status bar.

UNTOUCHABLE rewards staying unhurt. BERSERKER separately rewards killing
quickly.

### BERSERKER / SLAUGHTERHOUSE

`BERSERKER` and `SLAUGHTERHOUSE` are two tiers of one earned state that rewards
a rolling chain of server-confirmed kills. [Benheim Server
Support](../../../../server-mods/benheim-server-support/PRODUCT.md) owns kill
qualification. Six qualifying kills activate BERSERKER; twelve qualifying kills
replace it with SLAUGHTERHOUSE. Each qualifying kill resets the chain deadline
to 30 seconds after that kill. Kills seven through eleven refresh BERSERKER, and
kills after twelve refresh SLAUGHTERHOUSE. A 30-second gap expires the active
state and resets the chain.

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

After Valheim makes the current server connection available, Benheim starts
capability discovery. Benheim sends a Kill Attribution V3 capability request
and retries it for up to five seconds. Benheim accepts only a matching Kill
Attribution V3 capability response from that connection. If no matching
response arrives, the BERSERKER/SLAUGHTERHOUSE state remains inactive. Benheim
shows one center-screen warning per session and keeps the warning in the
Controls panel's Warnings block until a matching response arrives.

One confirmed defense can produce an adrenaline message and multiple
earned-state titles. Benheim combines them into one local Bonus text in the
order that the defense earns them. It also plays at most one charm cue for that
defense. This rule does not combine unrelated actions or callbacks.

The `0.1.76` candidate uses Valheim's distance-based playback for the native
charm cue. Compatible nearby players may hear it. Distant players must not hear
it.

None of the three states adds extra adrenaline. Existing perfect-defense
adrenaline behavior remains unchanged. Exact avoided-damage evidence may refine
CLUTCH later.

Bounded typed telemetry records CLUTCH healing at each native one-second tick.
For each UNTOUCHABLE tier activation, it records the first outgoing damage
payload modified by that tier. For each BERSERKER or SLAUGHTERHOUSE activation,
it records the first stamina-regeneration application and the first
physical-resistance application. Each record includes the native input,
configured modifier, and resolved output. The telemetry does not add per-frame
logging.

Automated checks cover:

- capability retries;
- registration of the effect and its loaded native icon in Valheim's current
  object database (`ObjectDB`);
- display in the native status bar;
- prevention of duplicate perfect-defense outcomes;
- reset after damage or intentional health costs;
- exclusion of passive maximum-health normalization from food expiry;
- UNTOUCHABLE thresholds for the streak shared by defenses and kills;
- rolling kill thresholds;
- tier replacement;
- selection and distance-based configuration of the native charm cue; and
- bounded typed telemetry for earned-state payloads.

Automated checks alone do not establish gameplay acceptance. The following
behavior still needs gameplay proof:

- one outcome per perfect defense;
- no reset from passive food normalization;
- reset from an intentional health cost;
- UNTOUCHABLE progression that mixes defenses and kills;
- BERSERKER activation at six qualifying kills;
- SLAUGHTERHOUSE activation at twelve qualifying kills;
- live multiplayer proof that compatible nearby players may hear the native
  charm cue, but distant players must not hear it; and
- the measured effect payloads.

[Adrenaline](../Adrenaline/PRODUCT.md) owns meter behavior and tuning.
[Weapon Rhythm](../WeaponRhythm/PRODUCT.md),
[Archery](../Archery/PRODUCT.md), and later combat mechanics can provide skill
signals. [Combat Feedback](../CombatFeedback/PRODUCT.md) owns local camera
response. [Affinities](../Affinities/PRODUCT.md) owns weapon-specific tradeoffs.
