# Product Review

This is the active ledger for Benheim behavior that is deployed for testing but
is not yet canonical. The owning `PRODUCT.md` defines the expected behavior.
This file records only the current proof boundary, regressions, and next useful
test. A behavior is canonical only when its owning `PRODUCT.md` lists it under
**Current Behavior**.

> **Tomorrow:** Talk through the operator-config papercuts from the `0.1.66`
> integration. The secret check first used the wrong scope instead of Doppler
> `valheim`/`prd`, and server status first selected the ignored `server.env`
> that the password guard rejects. This is only a reminder. Do not start cleanup
> before Ben and the project lead discuss it.

## Current candidate

- Accepted client baseline: `0.1.52`
- Installed private-test client: `0.1.67`
- Deployed Server Support: `0.1.5`
- Deployed Test Commands: `0.1.1`
- Required Server Support for this pass: `0.1.5`
- Required group version for this pass: Benheim `0.1.67`

## How proof works

Use the narrowest evidence that answers the actual product question:

- **Mechanical outcomes:** emit a typed event at the authoritative outcome or
  native modifier seam. Record the complete result, not every frame leading to
  it.
- **Stateful movement, AI, and physics:** use an explicitly armed, bounded test
  session. Capture meaningful transitions while it runs and emit one summary
  when it ends. Do not leave permanent per-frame logging enabled.
- **Presentation and feel:** Ben's observation is primary. Logs can prove that
  presentation was requested or placed, not that it looked or felt good.
- **Source and automated checks:** establish that a candidate is safe to test.
  They do not replace runtime or player evidence when the product claim is
  experiential.

Use these result labels:

- **Passed:** the named claim produced its expected evidence.
- **Failed:** evidence contradicted the claim.
- **Needs code:** the current candidate cannot honestly exercise or measure the
  claim.
- **Ready to test:** the current candidate already exposes the required
  evidence.
- **Needs bounded probe:** ordinary play and passive logs cannot efficiently
  establish the claim.

## Ready to test without more code

### Player Combat

- One qualifying perfect parry produces one confirmation, one adrenaline award,
  and one UNTOUCHABLE increment. The duplicate fix has automated proof but still
  needs this live regression check.
- Passive health normalization from food expiry leaves UNTOUCHABLE unchanged.
  Accepted damage and an intentional health cost each reset it.
- Six qualifying kills activate BERSERKER. Twelve within the rolling 30-second
  window replace it with SLAUGHTERHOUSE. Later kills refresh the current tier,
  and a 30-second gap expires it quietly. This test covers only the trigger and
  status lifecycle.
- Below 30 health, a perfect parry or dodge activates CLUTCH, shows its title and
  icon, and plays one native charm cue. This test covers only activation and
  presentation.
- Each server-confirmed qualifying hostile kill adds one point to the same
  untimed UNTOUCHABLE streak as a confirmed perfect defense. Mixed kills and
  defenses reach Tier I at five points, Tier II at eight, and Tier III at
  twelve. Accepted health loss and intentional health costs reset the streak.
- CLUTCH records each native one-second healing tick. Each UNTOUCHABLE tier
  records its first modified outgoing hit. Each BERSERKER or SLAUGHTERHOUSE
  activation records its first stamina-regeneration and physical-resistance
  result. Every payload record includes the native input, configured modifier,
  and resolved output. These events prove the measured payload, not its feel.
- Join after Kill Attribution V3 capability discovery and inspect the Controls
  warning state. No stale capability warning remains after the matching
  response arrives.
- Craft one ordinary item at a station that does not use Cooking. It keeps
  Valheim's native bonus behavior and emits the typed non-Cooking exclusion
  outcome.

### Diagnostics and developer tools

- Confirm Share Diagnostics starts enabled for each player and local readable
  logs plus `BenheimEvents.ndjson` continue to update.
- Turn Share Diagnostics off for one player. Remote forwarding stops while that
  player's local diagnostics continue.
- After world readiness, run `bh debug catalog effects heal`, `bh debug catalog
  text`, and `bh debug catalog ui toggle`. Each command reports matching counts
  and replaces the local runtime-catalog snapshot without sending catalog
  entries to Axiom.
- Run one catalog command before world readiness on a later launch. It must fail
  visibly rather than writing a misleading empty snapshot.

### Presentation and ordinary behavior

- Run `bh debug colliders off`; every overlay disappears immediately. Kill the
  spawned test Boars afterward so they do not remain in the shared world.
- Hit the outer head-centered collider and nearby body collider on the same
  creature to accept the refined headshot volume.
- While the local player is airborne, connect a supported melee attack with a
  Valheim `Character`. The player must descend at least `0.5 m/s` and move
  horizontally toward the contact point at least `7 m/s`. Only the attack's
  first authored contact with a `Character` produces an outcome. A qualified
  contact applies `1.15x` native damage and `3x` native stagger once. It also
  shows one `PERFECT IMPACT`, even when Benheim FX is off. Contacts remain
  native when the player is grounded, rising, or below the approach threshold.
  Contacts with terrain, destructibles, or gathering targets also remain
  native.
- Compare headshot, Cleave, mining AOE, and qualified Perfect Impact shake
  against their ordinary native impacts. Ben judges whether each feels
  distinct. Combat Shake and Benheim FX settings gate only Perfect Impact's
  shake, not its damage, stagger, outcome, or text.
- Exercise Put Away, Mass Repair, pocketing, and an active native top-left
  message together to accept the shared receipt lane.
- Run Put Away across several eligible chests. Existing terminal events record
  exactly five duration fields from a monotonic clock:

  1. Whole batch
  2. Aggregate scan and match
  3. Routing and owner handoff
  4. Owner mutation
  5. Requester settlement

  The timing fields must not affect routing, mutation, settlement, or
  completion.
- Hold `Left Shift` while planting with the Cultivator. The preview and
  placement form the same centered, deterministic 9x9 grid. Resource use,
  stamina, durability, spacing, cultivated-ground checks, creator ownership,
  effects, statistics, skill gain, rotation, and per-position preview validity
  remain native.
- Enter DANGEROUS and DEADLY once with FX enabled, then verify suppression with
  FX disabled.
- Cross the old 10-meter and new 20-meter comfort boundaries, including another
  room or floor.
- Capture requester settlement for an empty and a nearly full remote Windmill.
- Measure one Stone Oven recipe's bake and burn windows.
- Fill a Shield Generator from empty and from nearly full.
- Press `/` while chat and one menu are active. Each press must emit one terminal
  rejection without opening the console.
- Exercise Mass Repair with an undamaged aim, station denial, ward denial, and
  an exhausted tool.

## Testability gaps that need bounded probes

### Starred Boar behavior

Runtime events record the configured profile fields for live Boars. They do not
establish the behavior or feel that those fields produce. Casual observation is
too imprecise, and permanent AI logging would be noisy and expensive.

Add one command-armed Boar session with a short timeout and one test identifier.
Observe only nearby spawned zero-, one-, and two-star controls. Capture these
transitions and aggregates:

- distance and time at first alert;
- pursuit duration after the player breaks contact;
- total movement and turning during one charge;
- displacement after a tagged routine hit, heavy hit, and Boar shove;
- completion or one explicit incomplete reason for gates, slopes, water, or
  loss of path.

Add Perfect Impact displacement only after a usable Perfect Impact candidate
exists. Stopping, timing out, leaving the world, or disabling the session must
clear every observer. Emit one terminal summary per Boar and no per-frame
events. Ben still judges coherence, feel, and whether skilled counters remain
useful.

### Leech spawn opportunity

Natural observation across zone-owner changes is too sparse to prove a
three-times-as-frequent opportunity rate. This needs either a bounded spawn
simulation against the exact installed rules or a command-armed observation
window that records eligible checks, owner, roll, and outcome, then emits one
summary. Do not add permanent spawn-loop logging.

## Session notes

Record only a result that changes a decision or status above. Raw events,
screenshots, hashes, and query output stay in their owning systems.
