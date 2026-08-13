# Combat Feedback

Combat Feedback adds local camera response to native bow draw and outcomes that
Benheim already identifies. It does not decide whether Valheim accepted damage
or whether a target died.

## Current Behavior

- Drawing a native bow smoothly narrows the field of view by a modest amount.
  Ben confirmed that this bow-draw focus feels good in gameplay.

## In Development

- Native draw progress controls bow focus. Benheim does not change controls,
  projectile direction, draw timing, camera distance, or the base field of
  view.
- Bow focus works in Valheim's first-person view and at every third-person
  camera distance.
  It restores the native field of view after:

  - release or cancellation;
  - death or teleport;
  - a cutscene, attachment, or free-fly; and
  - player replacement or plugin shutdown.

- The Shift+B menu has a `Benheim Config` page with four persisted native-style
  checkboxes:

  - `Benheim FX` is the master switch.
  - `Bow Focus` controls the bow-draw field-of-view effect.
  - `Combat Shake` controls all approved headshot, Cleave, mining AOE, and
    Perfect Impact shake requests together.
  - `Danger Arrival FX` controls the banner, stinger, and brief edge vignette
    together.

- When `Benheim FX` is off, it overrides all three effect families without
  changing their saved preferences. Turning it back on restores those
  preferences. Every change applies without a restart.
- Turning `Benheim FX` or Bow Focus off immediately starts a smooth return to
  the native field of view.
  Turning Combat Shake off suppresses future Benheim requests. It does not stop
  or change an active native camera shake.
- Turning Danger Arrival FX off suppresses future arrival effects. An effect
  that already started can finish. Danger labels, spawn rates, combat behavior,
  and native Valheim effects remain unchanged.

- Benheim requests camera shake only when it qualifies a headshot, applies one
  Woodcutting Cleave, starts one Mining area-of-effect (AOE) action with
  secondary hit areas, or qualifies one Weapon Rhythm Perfect Impact outcome.
- Cleave and AOE request one shake for the outcome. Their secondary target or
  hit-area loops do not request more shakes.
- Cleave, mining AOE, and headshot shake still need a gameplay retest. Ben found
  the prior `1.75` Cleave and mining AOE strength too strong. The next candidate
  uses raw Valheim `AddShake` strengths of `1.35` for both, compared with
  Valheim's `1.2` ordinary axe-impact request. It raises headshot shake from
  `0.45` to `1.1` to test whether the smaller feedback is readable. Perfect
  Impact starts at `1.25`: above ordinary axe impact but below the big-impact
  requests. All values remain experimental.
- Rapid requests keep the strongest eligible shake during a short coalescing
  window. Every shake stays under one shared cap and uses Valheim's native
  camera-shake preference.
- Headshot shake confirms Benheim's local collision-time qualification. Each
  Cleave, AOE, or Perfect Impact shake confirms its local Benheim outcome. None
  of these signals claims that the target's owner authoritatively confirmed
  damage or death.
- Benheim does not shake the camera for:

  - ordinary hits;
  - confirmed kills;
  - parries;
  - staggers;
  - damage over time;
  - misses;
  - arrow release; or
  - arrow flight.
- Combat Feedback adds no result RPC, hitstop, music, weather, environmental
  effect, audio asset, visual asset, persistent gameplay state, or world
  mutation.
  It adds no persistent danger ambience, lighting, fog, low-health vignette, or
  other presentation behavior.

The current tuning is experimental. One shared set of values controls the
bow-focus curve and timing, shake strengths, shared cap, and coalescing window
for the next combined gameplay test.

## Future Ideas

- Confirmed-kill punctuation is deferred. Benheim will not infer or guess kills
  on the attacker's client. Valheim assigns each target an owner that determines
  whether it died. Honest kill feedback on the attacker's client requires an
  explicit confirmation protocol from every possible target owner. Ben deferred
  the protocol and the punctuation. Reconsider both only if the punctuation's
  gameplay value justifies the protocol's multiplayer complexity.
- Ben proposed a low-health vignette, a visual effect on the local client, as a
  future idea. Its behavior remains open.
