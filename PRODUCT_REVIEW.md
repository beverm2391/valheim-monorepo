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
- Installed private-test client: `0.1.66`
- Deployed Server Support: `0.1.4`
- Deployed Test Commands: `0.1.1`
- Required group version for this pass: Benheim `0.1.66`

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

## Passed claims in `0.1.66`

- **Put Away durability passed in live multiplayer.** The owner-authoritative
  path survived rapid repeated use, both requester/current-owner arrangements,
  simultaneous contention followed by immediate reuse, and exact
  accepted/refunded settlement. Players observed no loss, duplication,
  permanent lease, or chest disagreement. Crash or reconnect during an
  in-flight reservation remains intentionally unsupported.
- **Remote typed-event delivery passed for the active group.** Axiom received
  typed events from Benaldson, JayTrain, and GlIzZy. Queries by player, client,
  session, event, and Put Away operation work.
- **Kill Attribution V3 passed.** Matching capability responses and
  confirmed-kill delivery reached active clients. The former false-warning race
  did not recur.
- **UNTOUCHABLE activation, reset, and status presentation passed.** The streak
  reached five, eight, and twelve without health loss. Tier I appeared in the
  native status bar; Tier II and Tier III replaced their predecessors. Ben saw
  all three transitions and accepted the presentation. Native parry chip
  damage correctly reset the streak.
- **Boar spawning and collider-overlay presentation passed.** `bh spawn boar
  0|1|2` created the requested native tiers. Ben accepted how the cyan collider
  overlay looked on live starred Boars. Runtime events recorded the configured
  visual scale, capsule, path-agent, perception, pursuit, speed, turning, and
  push fields for live Boars on active clients.
- **Cooking bonus rolls passed.** Runtime events captured
  successful and failed native bonus rolls with the configured base chance,
  native skill factor, effective chance, consumed roll, and actual native
  result count. Ben observed and accepted the bonuses.
- **Native `/` opening passed.** Normal-gameplay presses opened the available,
  enabled native console and produced exactly one typed terminal outcome per
  press.

## Needs code before the next test

| Feature | Current boundary | Next candidate |
| --- | --- | --- |
| Perfect Impact | **Failed for the tested primary attacks.** Their airborne starts were rejected, while accepted attacks were buffered until grounded. Repeating that technique has no decision value. | Qualify at the authored `Character` contact. Require the player to be airborne, descending at `0.5 m/s` or faster, and moving toward the target at least `7 m/s`. Keep one outcome per attack, `1.15x` damage, and `3x` stagger. |
| Earned-buff payloads | **Needs code.** Status application and native status-bar presence do not prove the bonuses. | Add shared bounded payload telemetry. Record actual CLUTCH healing ticks, the first modified UNTOUCHABLE hit in each tier, and the first stamina-regeneration and resistance result in each BERSERKER or SLAUGHTERHOUSE activation. |
| UNTOUCHABLE progression | **Needs code.** Version `0.1.66` counts only confirmed perfect defenses. | Add one point for each server-confirmed qualifying hostile kill. Keep the shared untimed streak, 5/8/12 thresholds, and reset on accepted health loss or intentional health cost. |
| Put Away timing profile | **Needs code.** Batch start and finish events show nontrivial wall-clock time but do not locate the cost. | Record bounded timings for scanning, routing, owner mutation, and settlement. Optimize only a measured bottleneck without weakening durability. Add immediate loading feedback only if the measured delay remains visible. |
| Cultivator grid | **Needs code.** Mass planting is fixed at 5x5. | Change the fixed grid to 9x9 while preserving native resources, stamina, durability, spacing, cultivated-ground checks, ownership, effects, skill gain, and preview behavior. |

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
- Compare headshot, Cleave, and mining AOE shake against their ordinary native
  impacts. Ben judges whether each feels distinct. Test Perfect Impact shake
  only after the replacement candidate can produce a qualified hit.
- Exercise Put Away, Mass Repair, pocketing, and an active native top-left
  message together to accept the shared receipt lane.
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
