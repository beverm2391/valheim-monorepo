# Combat Feedback

Combat Feedback adds local camera response to outcomes that Benheim already
identifies. It does not decide whether Valheim accepted damage or whether a
target died.

## Current Behavior

- No Combat Feedback behavior has been gameplay-confirmed yet.

## In Development

- Drawing a native bow smoothly narrows the field of view by a modest amount.
  Native draw progress controls the focus. Benheim does not change controls,
  projectile direction, draw timing, camera distance, or the base field of
  view.
- Bow focus works with Valheim's first-person and third-person camera distance.
  It restores the native field of view after:

  - release or cancellation;
  - death or teleport;
  - a cutscene, attachment, or free-fly; and
  - player replacement or plugin shutdown.

- Benheim requests camera shake only when it qualifies a headshot, applies one
  Woodcutting Cleave, or starts one Mining area-of-effect (AOE) action with
  secondary hit areas.
- Cleave and AOE request one shake for the outcome. Their secondary target or
  hit-area loops do not request more shakes.
- Rapid requests keep the strongest eligible shake during a short coalescing
  window. Every shake stays under one shared cap and uses Valheim's native
  camera-shake preference.
- Headshot shake confirms Benheim's local collision-time qualification. Each
  Cleave or AOE shake confirms its local Benheim outcome. None of these signals
  claims that the target's owner authoritatively confirmed damage or death.
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
  effect, audio asset, visual asset, persistent state, or world mutation.

The first tuning is experimental. One code owner contains the bow-focus curve,
focus timing, shake strengths, shared cap, and coalescing window for the next
combined gameplay test.

## Future Ideas

- Confirmed-kill punctuation is deferred. Benheim will not infer or guess kills
  on the attacker's client. Valheim assigns each target an owner that determines
  whether it died. Honest kill feedback on the attacker's client requires an
  explicit confirmation protocol from every possible target owner. Ben deferred
  the protocol and the punctuation. Reconsider both only if the punctuation's
  gameplay value justifies the protocol's multiplayer complexity.
- Ben proposed a low-health vignette, a visual effect on the local client, as a
  future idea. Its behavior remains open.
