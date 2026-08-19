# Product Review

This file is the active ledger for Benheim behavior that is deployed for
testing but is not yet canonical. The owning `PRODUCT.md` defines the expected
behavior. This file records version boundaries, regressions, evidence, and the
next player proof.

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

Automated, source, or runtime evidence does not equal gameplay acceptance.
Acceptance requires the named player-visible or multiplayer result. When Ben
accepts a behavior, move it to **Current Behavior** in the owning product file
and remove its active row here.

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

### 1. Put Away conservation and convergence

Owner: [Inventory](mods/benheim-qol/src/Inventory/PRODUCT.md) and the
[owner-authoritative protocol](shared/benheim-inventory-protocol/PROTOCOL.md).

Classification: regression fix. The deployed `0.1.64` path could finish exact
item settlement but retain the completed batch and global lease. Earlier
requester-local designs also produced stale writes and item loss. Version
`0.1.65` changes completion cleanup and rejects incompatible peer cohorts
before reservation.

Status: **Not run**.

Prepare one isolated test chest with no other nearby eligible chest. Put a
small amount of Wood or Stone in it so Put Away considers it eligible. Each
player records the exact matching-item count in inventory and the chest before
each case.

- [ ] **Single requester.** One player uses `Left Shift + P`. Every player then
  opens the chest and sees the same final count. The receipt matches the moved
  amount, and the requester loses exactly that amount.
- [ ] **Stale-view sequence.** Player B opens and closes the chest first.
  Player A deposits with Put Away. Without B reopening the chest, B immediately
  deposits with Put Away. The final chest contains `base + A + B`, every peer
  sees that result without a take-and-reinsert refresh, and both inventories
  conserve their items.
- [ ] **Reverse authority.** Repeat the stale-view sequence with A and B
  reversed. The same conservation and convergence results must hold with
  either player as the requester or current chest owner.
- [ ] **Simultaneous contention.** Count down and have A and B use Put Away at
  the same time. Exactly one player proceeds. The other sees
  `Put Away busy — retry in a few seconds` before scanning or moving items.
- [ ] **Partial capacity.** Fill every chest slot and leave room for exactly
  two units in one matching stack. Give the requester ten units. The chest
  accepts two, eight remain or return to the requester, and nothing drops or
  disappears while inventory has refund room.
- [ ] **Immediate reuse.** After each completion or busy response, start
  another ordinary Put Away. Neither result may leave the global lease or
  batch stuck.

Pass requires exact item conservation and immediate peer convergence in every
case. A correct receipt without matching inventories and chest state is a
failure.

### 2. Server capability and earned combat states

Owner: [Player Combat](mods/benheim-qol/src/PlayerCombat/PRODUCT.md) and
[Server Support](server-mods/benheim-server-support/PRODUCT.md).

Classification: failed-candidate stabilization. Version `0.1.64` could show a
false Server Support warning because capability discovery raced connection
readiness. UNTOUCHABLE could activate without proving persistent native
status-bar presence.

Status: **Not run**.

- [ ] Every player joins the updated server and waits five seconds. The
  Controls menu shows no BERSERKER Server Support warning.
- [ ] Below 30 health, complete one perfect parry or perfect dodge. `CLUTCH!`
  appears in the defense feedback, the native charm cue plays once, the
  Lingering Healing Mead icon appears, and health recovers 60 over six seconds.
- [ ] Complete five mixed perfect parries or perfect dodges without losing
  health. `UNTOUCHABLE!` appears once and one indefinite Wolf Sight icon remains
  in the native status bar.
- [ ] At eight defenses, `UNTOUCHABLE II!` appears and the outgoing damage
  bonus becomes 20%. At twelve defenses, `UNTOUCHABLE III!` appears and the
  bonus becomes 30%. One indefinite Wolf Sight icon remains throughout.
- [ ] Take any actual health loss. The icon disappears quietly and the streak
  resets. A blocked hit that causes no health loss does not reset it.
- [ ] Kill three qualifying hostile monsters with no more than ten seconds
  between consecutive kills. `BERSERKER!` appears with one Crystal Heart icon.
  Continue the same chain to six kills. `SLAUGHTERHOUSE!` replaces the first
  tier instead of stacking with it.
- [ ] Let the kill window expire. The earned state disappears quietly. A later
  chain can activate again.

The three earned states remain experimental even if their mechanics work.
Ben separately judges whether their trigger difficulty, strength, icons, text,
and charm cue feel good.

### 3. Diagnostics and runtime discovery

Owner: [Benheim](mods/benheim-qol/PRODUCT.md) and
[Shortcuts](mods/benheim-qol/src/Shortcuts/PRODUCT.md).

Classification: unproven integration and new developer tooling.

Status: **Not run**.

- [ ] Each player enables **Share Diagnostics** and produces at least one
  Player Combat or Put Away event. Local readable logs and
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

### 4. Starred Boar experiment

Owner: [Enemy Tiers](mods/benheim-qol/src/EnemyTiers/PRODUCT.md) and
[Test Commands](server-mods/benheim-test-commands/PRODUCT.md).

Classification: failed-candidate tuning and unproven debug presentation.

Status: **Not run in the current candidate**.

- [ ] Run `bh help`, then `bh spawn boar 0`, `bh spawn boar 1`, and
  `bh spawn boar 2`. Each command creates the requested native tier exactly
  once near the administrator.
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

| Behavior | Classification | Current evidence | Next player proof | Status |
| --- | --- | --- | --- | --- |
| Perfect Impact attack-start momentum and visible text | Failed candidate | Earlier rule applied in logs; current start gate is automated only | Sprint-jump, start a melee swing airborne, connect while descending, then judge text, shake, damage, and stagger | Not run |
| Headshot exact collider volume | Candidate refinement | Geometry automated; older global headshots accepted | Hit outer head-centered collider and nearby body collider on the same creature | Not run |
| Headshot, Cleave, mining AOE, and Perfect Impact shake | Candidate tuning | Native call sites and strengths verified | Compare each outcome with its ordinary native impact and judge distinction | Not run |
| Shared top-left receipt lane | Candidate presentation | Layout automated | Exercise Put Away, Mass Repair, pocketing, and an active native top-left message | Not run |
| Small-minimap title-case danger label | Candidate presentation | Earlier white treatment accepted; final alignment unaccepted | Cross biomes and portal once; verify biome/category pairing and right alignment | Not run |
| Dangerous-area edge flash | Failed candidate presentation | Arrival logic runtime-proven; visual cue unaccepted | Enter DANGEROUS and DEADLY once with FX on, then verify suppression with FX off | Not run |
| Cooking bonus chance | New balance behavior | Source-level proof | Craft and retrieve enough Cooking outputs to observe bonuses and verify non-Cooking crafts stay native | Not run |
| Comfort range at 20 meters | New balance behavior | Source-level proof | Move across the old 10-meter and new 20-meter boundaries, including another room or floor | Not run |
| Remote station batch fill | Candidate multiplayer behavior | Some local and remote station evidence exists | Empty and nearly full remote Windmill; compare accepted, refunded, inventory, and station counts | Not run |
| Stone Oven timing and diagnostics | Candidate multiplayer behavior | Prior owner/timing logs only | Measure bake and burn windows for one recipe under the current owner | Not run |
| Shield Generator batch fuel | New production path | Focused automated proof | Fill from empty and nearly full with exact inventory counts | Not run |
| `/` native-console shortcut | New shortcut | Source-level proof | Enable native console, test gameplay opening, and confirm no action during chat or menus | Not run |
| Mass Repair denial and zero-result cases | Coverage gap | Main repair flow accepted | Test undamaged aim, station denial, ward denial, and exhausted tool cases | Not run |
| Three-times-as-frequent Leech opportunities across zone owners | Candidate multiplayer balance | Source-level proof | Observe compatible clients exchanging zone ownership during an eligible spawn period | Not run |

## Session results

Add only decision-changing player observations here. Keep raw logs, event
records, screenshots, hashes, and query output in their owning systems. Each
entry states the version, scenario, observed result, and whether the row passed,
failed, or remained blocked.
