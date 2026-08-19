# Product Review

This file is the active ledger for Benheim behavior that is deployed for
testing but is not yet canonical. The owning `PRODUCT.md` defines the expected
behavior. This file records version boundaries, regressions, current evidence,
and the next proof.

A behavior is canonical only when its owning `PRODUCT.md` lists it under
**Current Behavior**. A noncanonical behavior shipped in a test candidate must
appear here. An omitted candidate is a tracking defect, not implicit
acceptance. Future ideas and unresolved product choices do not belong here
until Ben approves them for implementation or testing.

## Review state

- Accepted client baseline: `0.1.52`
- Installed private-test client: `0.1.65`
- Deployed Server Support: `0.1.3`
- Deployed Test Commands: `0.1.1`
- Required group version for this pass: Benheim `0.1.65`

Use one result status:

- **Not run**: no player evidence for this candidate.
- **Passed**: the named scenario produced the expected observable result.
- **Failed**: the named scenario contradicted the product contract.
- **Blocked**: the scenario could not reach the behavior under review.

If a failed result breaks previously accepted behavior, also classify it as a
regression.

Use typed runtime events as the primary proof of mechanical outcomes, network
settlement, random outcomes, and state transitions. Ben's observations
corroborate that evidence. His observations remain the primary acceptance
evidence for visual presentation and feel. Source and automated proof can
establish that a candidate is safe to test, but they do not replace the
evidence required by the behavior under review. When Ben accepts a behavior,
move it to **Current Behavior** in the owning product file and remove its active
row here.

## Stop conditions

Stop Put Away testing immediately if an item disappears, duplicates, or is not
visible to every connected player. Do not interact with the affected chest
again until its evidence is captured. Record the approximate time, player,
item, starting count, and intended chest. Local typed diagnostics stay enabled
even when remote sharing is off.

Use disposable materials for inventory and production tests. Do not test a
crash or disconnect during an in-flight Put Away reservation. That recovery
case is explicitly unsupported.

## `0.1.65` critical pass

### 1. Server capability and earned combat states

Owner: [Player Combat](mods/benheim-qol/src/PlayerCombat/PRODUCT.md) and
[Server Support](server-mods/benheim-server-support/PRODUCT.md).

Classification: failed-candidate stabilization. Version `0.1.64` could show a
false Server Support warning because capability discovery raced connection
readiness. UNTOUCHABLE could activate without proving persistent native
status-bar presence.

Status: **Failed**.

Observed in `0.1.65`:

- **Passed — player-visible BERSERKER activation.** Ben saw `BERSERKER!` appear
  during live combat and accepted its native status-bar icon. Separately, local
  typed events recorded the native effect applying, appearing in Valheim's HUD
  list, refreshing on later qualifying kills, and expiring after the chain
  window. Tier I's configured 25% physical damage reduction and 50% stamina
  regeneration have not been measured in gameplay. Expiry presentation and the
  SLAUGHTERHOUSE tier also remain unproven.
- **Failed — duplicate perfect-parry outcome.** One blocked Troll attack
  produced two `perfect_defense_confirmed` events milliseconds apart. The two
  outcomes produced two `Perfect parry` messages and two `+10` adrenaline
  awards. A later Troll attack reproduced the same duplicate. The duplication
  occurred before presentation; it was not one outcome rendered twice.
- **Failed — UNTOUCHABLE damage reset.** Axiom recorded periodic food-expiry
  health normalization as `player_damage_accepted`, followed by
  `untouchable_reset`. Passive maximum-health normalization must not count as
  actual harm. The observed one-second clamps included `26.4453 → 26.2798`,
  `26.2798 → 26.0395`, and `26.0395 → 25`. Source tracing shows that food expiry
  lowers maximum health through a passive health update rather than actual
  damage. Ben also triggered UNTOUCHABLE and saw its icon appear, but it did not
  remain in the native status bar. The replacement observer now counts actual
  damage and intentional health costs and ignores this passive change. The
  duplicate-outcome fix and food-normalization reset fix have automated proof
  only; both still need the gameplay retest below.

- [ ] Every player joins the updated server and waits five seconds. The
  Controls menu shows no BERSERKER Server Support warning.
- [ ] One blocked Troll attack that qualifies as a perfect parry produces one
  `perfect_defense_confirmed` event, one `Perfect parry` message, one `+10`
  adrenaline award, and one UNTOUCHABLE streak increment.
- [ ] Below 30 health, complete one perfect parry or perfect dodge. `CLUTCH!`
  appears in the defense feedback, the native charm cue plays once, the
  Lingering Healing Mead icon appears, and health recovers 60 over six seconds.
- [ ] Complete five mixed perfect parries or perfect dodges without losing
  health. `UNTOUCHABLE!` appears once and one indefinite Wolf Sight icon remains
  in the native status bar.
- [ ] At eight defenses, `UNTOUCHABLE II!` appears and the outgoing damage
  bonus becomes 20%. At twelve defenses, `UNTOUCHABLE III!` appears and the
  bonus becomes 30%. One indefinite Wolf Sight icon remains throughout.
- [ ] Take actual damage and then an intentional health cost. Each clears the
  icon and resets the streak. A blocked zero-damage contact and passive
  maximum-health normalization caused by food expiry leave both unchanged.
- [ ] Kill six qualifying hostile monsters, with each consecutive kill arriving
  before the 30-second deadline. `BERSERKER!` appears with one Crystal Heart
  icon. Kills seven through eleven refresh it. Continue the same chain to twelve kills;
  `SLAUGHTERHOUSE!` replaces the first tier instead of stacking with it. Later
  qualifying kills refresh SLAUGHTERHOUSE.
- [ ] Wait until 30 seconds have passed after the latest qualifying kill. The
  earned state disappears quietly, the chain resets, and a later chain can
  activate again.

The three earned states remain experimental even if their mechanics work.
Ben separately judges whether their trigger difficulty, strength, icons, text,
and charm cue feel good.

### 2. Diagnostics and runtime discovery

Owner: [Benheim](mods/benheim-qol/PRODUCT.md) and
[Shortcuts](mods/benheim-qol/src/Shortcuts/PRODUCT.md).

Classification: unproven integration and new developer tooling.

Status: **Passed** for JayTrain remote forwarding; remaining scenarios are
**Not run**.

After Ben re-enabled Share Diagnostics for JayTrain, Axiom received that
player's Player Combat events. Automated checks cover the one-time legacy-config
migration and persistence after a later opt-out. Other players, complete
typed-field queries, opt-out behavior, and the runtime catalog commands remain
unproven.

- [ ] Each player confirms **Share Diagnostics** starts enabled and produces at
  least one Player Combat or Put Away event. Local readable logs and
  `BenheimEvents.ndjson` continue to update.
- [ ] Query Axiom by player, client, session, event, and Put Away operation ID.
  Every typed field defined on each event must arrive without requiring friends
  to export logs.
- [ ] Turn **Share Diagnostics** off for one player. Remote forwarding stops
  while that player's local diagnostics continue.
- [ ] After the world is ready, run `bh debug catalog effects heal`,
  `bh debug catalog text`, and `bh debug catalog ui toggle`. Each command
  reports matching counts and stable donor identities. It replaces the local
  `BenheimRuntimeCatalog.ndjson` snapshot without sending catalog entries to
  Axiom.
- [ ] Run one catalog command before world readiness on a later launch. It must
  fail visibly with the missing runtime prerequisite instead of writing a
  misleading empty snapshot.

### 3. Starred Boar experiment

Owner: [Enemy Tiers](mods/benheim-qol/src/EnemyTiers/PRODUCT.md) and
[Test Commands](server-mods/benheim-test-commands/PRODUCT.md).

Classification: failed-candidate tuning and unproven debug presentation.

Status: **Passed** for the spawn commands; collider and tuning scenarios are
**Not run**.

- [x] Run `bh help`, then `bh spawn boar 0`, `bh spawn boar 1`, and
  `bh spawn boar 2`. Each command creates the requested native tier exactly
  once near the administrator. Ben proved these spawn commands in gameplay.
- [ ] Run `bh debug colliders on`. For each Boar, compare its visible body and
  head area with its cyan capsule while it stands, turns, charges, and attacks.
- [ ] Compare ordinary, one-star, and two-star detection, pursuit, turning,
  shove, routine knockback resistance, bite reach, gates, slopes, and water.
  Heavy attacks and Perfect Impact must remain useful counters.
- [ ] Run `bh debug colliders off`. Every overlay disappears immediately.
- [ ] Kill the spawned test Boars after the review so they do not remain in the
  shared world.

## Carryover review ledger

These behaviors are not required to diagnose Put Away first. They remain
noncanonical and must not disappear from later Product Review passes.

| Behavior | Classification | Current evidence | Next proof | Status |
| --- | --- | --- | --- | --- |
| Perfect Impact attack-start momentum and visible text | Failed candidate | One earlier Lox operation emitted `airborne_melee_armed` with `start_forward_speed=8.068`, followed by `airborne_melee_applied` at `vertical_speed=-1.505`; feedback was placed and shake triggered. Ben's recent attempts emitted no new Weapon Rhythm events and produced no visible result. | Start a fresh scoped diagnosis of why current melee starts do not enter the typed attack-start path before asking Ben to repeat the technique | Blocked |
| Headshot exact collider volume | Candidate refinement | Geometry automated; older global headshots accepted | Hit outer head-centered collider and nearby body collider on the same creature | Not run |
| Headshot, Cleave, mining AOE, and Perfect Impact shake | Candidate tuning | Native call sites and strengths verified | Compare each outcome with its ordinary native impact and judge distinction | Not run |
| Shared top-left receipt lane | Candidate presentation | Layout automated | Exercise Put Away, Mass Repair, pocketing, and an active native top-left message | Not run |
| Dangerous-area edge flash | Failed candidate presentation | Arrival logic runtime-proven; visual cue unaccepted | Enter DANGEROUS and DEADLY once with FX on, then verify suppression with FX off | Not run |
| Cooking bonus chance | New balance behavior | Ben observed cooking bonuses and said the result seems good; the bonus-roll path emits no typed event, so the configured chance and exact extra count cannot be accepted from logs | Add one typed roll/result event, then corroborate ordinary Cooking and one non-Cooking craft without grinding random samples | Blocked |
| Comfort range at 20 meters | New balance behavior | Source-level proof | Move across the old 10-meter and new 20-meter boundaries, including another room or floor | Not run |
| Remote station batch fill | Candidate multiplayer behavior | Axiom recorded one remote Windmill owner accepting `50/50` with `result=complete`; Ben saw the fill work | Capture requester settlement for an empty and nearly full Windmill, including accepted, refunded, inventory, and station counts | Not run |
| Stone Oven timing and diagnostics | Candidate multiplayer behavior | Prior owner/timing logs only | Measure bake and burn windows for one recipe under the current owner | Not run |
| Shield Generator batch fuel | New production path | Focused automated proof | Fill from empty and nearly full with exact inventory counts | Not run |
| `/` native-console shortcut suppression | New shortcut | Ben accepted normal gameplay opening; the chat, menu, password, and text-input suppression guards emit no typed result | Add bounded shortcut-decision diagnostics, then confirm that `/` does nothing during chat and one menu | Not run |
| Mass Repair denial and zero-result cases | Coverage gap | Main repair flow accepted | Test undamaged aim, station denial, ward denial, and exhausted tool cases | Not run |
| Three-times-as-frequent Leech opportunities across zone owners | Candidate multiplayer balance | Source-level proof | Observe compatible clients exchanging zone ownership during an eligible spawn period | Not run |

## Session results

Add only decision-changing player observations here. Keep raw logs, event
records, screenshots, hashes, and query output in their owning systems. Each
entry states the version, scenario, observed result, and whether the row passed,
failed, or remained blocked.
