# Put Away Owner-Authoritative Protocol

## Mental Model

A Valheim chest has one authoritative owner. Every other in-memory chest
inventory is a cache, even when that peer has observed the latest ownership or
data-revision number. Put Away is safe only when the current owner validates
and changes its authoritative inventory, then returns a correlated accepted
result to the requester.

The requester reserves items before sending the immutable request. It restores
only the rejected remainder after a matching result. The dedicated server
routes but does not invent chest contents. The global Put Away lease prevents
two Benheim batches from running concurrently; it does not make a cached chest
fresh.

## Durability Contract

- Only the current chest owner may apply a deposit.
- One immutable transaction identity and payload describe one deposit.
- While the requester stays connected, a correlated retry cannot apply that
  deposit twice.
- The requester removes the reservation before the owner writes.
- The requester returns each rejected item to its inventory and does not return
  accepted items. As an emergency fallback only, if the requester inventory
  cannot accept a rejected item during settlement, Put Away drops that exact
  item nearby instead of discarding it.
- Chest contents, requester contents, and any explicit refund drop conserve the
  exact item counts.
- Connected peers converge on the owner's committed chest contents.
- An ambiguous response remains pending and correlated while the session stays
  connected. It never becomes an uncorrelated local retry.
- Exact requester settlement completes each deposit. The batch becomes terminal
  and Put Away releases the global lease only after scheduling stops and every
  reserved deposit settles. Receipt cleanup cannot keep the batch active or
  delay release of the global lease.
- Before Put Away scans chests or reserves items, every connected peer must
  announce the current Put Away generation: lease `v2` and transaction `v4`.
- The server records the connected-peer cohort at lease grant and validates the
  same cohort before each container reservation. A join, disconnect, or
  readiness change stops the batch before its next reservation. The lease
  remains with the holder until the holder releases it.
- Writing diagnostic events is best effort. A diagnostic failure cannot interrupt
  mutation, result delivery, settlement, completion, or receipt cleanup.

## Flow

1. Benheim Server Support grants the requester the global Put Away lease only
   after every connected peer announces the current Put Away generation.
   Scanning starts after the grant.
2. Before each container reservation, the requester asks the server to validate
   the active lease and its connected-peer cohort. If the server rejects
   validation, the requester stops the batch before that reservation.
3. After validation, the requester selects a chest, creates an immutable
   transaction ID, chest ID, request payload, and payload hash, then reserves
   the selected source stacks in memory before sending the request.
4. The requester can validate the cohort and reserve a later eligible chest
   while earlier transactions remain in flight. The transaction API removes
   source items synchronously during each reservation, so later reservations
   cannot include the same items.
5. The server deduplicates the transaction, resolves the chest's current owner,
   and routes the request to that peer.
6. The owner confirms ownership and validates the requester, access, distance,
   item match, chest use, and live capacity.
7. The owner applies accepted items to its inventory, records a receipt on the
   chest ZDO, and returns accepted counts through the server.
8. The server accepts a result only from the latest owner it resolved. The
   requester then requires a matching transaction ID and payload hash. It
   settles every accepted, refunded, or emergency-dropped item exactly once.
   Results can settle in any order. A failed cohort validation stops new
   reservations but does not cancel deposits already in flight. The batch
   becomes terminal and Put Away releases the global lease only after
   scheduling stops and every reserved deposit settles.
9. After each deposit completes, the requester sends exactly one best-effort,
   one-way receipt cleanup request. It keeps no cleanup state and does not
   retry. The server requires the completed correlation and original routed sender, then
   forwards the request to the chest's current owner. The owner sends no
   cleanup confirmation through the server. A lost or rejected cleanup does
   not repeat settlement, reopen completion, delay callbacks, or retain the
   batch or lease.

## Traceability

| Protocol component | Invariant protected | Concrete failure if removed | Proving test |
| --- | --- | --- | --- |
| Global server lease | At most one Benheim Put Away batch enters scanning and mutation | Two requesters mutate replicated chest snapshots concurrently | `Put Away lease exclusion checks passed` |
| Every connected peer announces the current Put Away generation, and the server validates the recorded connected-peer cohort before each container reservation | Put Away stops before scanning or reservation if any connected peer has not announced the generation; a cohort change stops the batch before its next reservation while the lease remains with the holder until the holder releases it | A legacy peer enters mutation, then disagrees about cleanup and retains the lease or fills the receipt ledger | `Put Away mixed-version pre-reservation compatibility checks passed` plus after-grant cohort-change controls |
| Immutable transaction ID plus payload hash | Results and retries attach only to their request | A stale or conflicting result refunds or commits the wrong reservation | `Put Away owner-authoritative stale-payload integration checks passed` |
| Server routes to the current owner and accepts only the latest resolved owner's result; requester validates the transaction ID and payload hash | Only authoritative contents are read and changed | A requester overwrites a newer chest with its stale cache, or a delayed old-owner result settles after rerouting | `Put Away owner-authoritative stale-payload integration checks passed` |
| Owner receipt and server completion cache | A retry applies at most once across routing and ownership changes | Lost responses duplicate accepted items | receipt codec test plus the connected-retry integration case |
| Accepted counts and requester remainder restoration | Player, chest, and explicit refund-drop item counts are conserved | Partial capacity loses rejected items or duplicates accepted items | stale-payload, partial-capacity, and filled-inventory refund cases |
| Exact settlement before completion | Completion reflects every accepted, refunded, or emergency-dropped item | Early completion hides an unsettled remainder and can lose or duplicate items | source ordering guard, filled-inventory refund case, and receipt-acknowledgement wire/liveness case |
| Pipelined batch drain | Put Away can schedule independent owner-authoritative chest transactions before earlier transactions settle. The batch becomes terminal and Put Away releases the global lease only after scheduling stops and every reserved deposit settles. | An out-of-order result, failed validation, or throwing callback releases the lease while another reservation is still in flight | `Put Away pipelined batch scheduler checks passed` |
| One-way current-owner receipt cleanup | Completed receipt entries are normally reclaimed without becoming a transaction liveness gate | Removing cleanup can eventually exhaust receipt capacity; confirmed cleanup adds state and failure modes without protecting item integrity | receipt-acknowledgement wire/liveness case |
| Best-effort typed transaction events | Requester, router, and owner decisions can be correlated, and diagnostic failures cannot interrupt mutation, result delivery, settlement, completion, or receipt cleanup | A throwing sink interrupts delivery or completion, or repeats an emergency refund drop | throwing-sink and post-drop controls plus `inventory transaction typed diagnostic schema checks passed` |

Use these invariant names in tests and reviews. A test must include the unsafe
control when the failure can otherwise pass under both implementations.

## Rejected Shortcuts

### Requester-side native Stack All

`Container.RPC_StackResponse` invokes `Inventory.StackAll` on the requester.
That requester may hold a stale chest payload. A successful local write and a
new local revision do not prove that it extended the authoritative contents.

### Ownership or revision wait as freshness proof

Valheim tracks owner and data revisions separately. A peer can accept a newer
owner revision while declining an equal data revision, leaving its older
payload in memory. Equal revision numbers do not prove equal bytes.

### Global lease as freshness proof

The lease prevents overlapping Benheim operations. It does not prevent manual
chest ownership changes, delayed replication, or a later requester starting
from an equal-revision stale payload.

## Confirmed Regression

Benheim `0.1.62` reproduced the exact unsafe state with three clients. Client A
completed an owner-transitioning deposit. Client B then observed A's new data
revision but retained the pre-A in-memory counts. B's requester-local Stack All
wrote its stale base plus B's items, overwriting A's accepted items. A third
client was correctly rejected by the global lease, proving that serialization
and freshness are separate invariants.

The executable stale-payload integration case uses those item counts without
embedding private player, world, path, or log data. Its controlled local-write
path must reproduce the overwrite, while the owner-routed path must end at
base + A + B on the owner and both replicas with exact conservation.

## Proof Status

The earlier owner-routed protocol was gameplay-proven with the current chest
owner applying each deposit and gave immediate two-client visibility before it
was removed. Its retry, partial-capacity, protocol-mismatch, and interrupted
recovery behavior remained in development.

Benheim `0.1.64` with Server Support `0.1.2` proved owner mutation, result
forwarding, and exact requester settlement in live multiplayer. It also exposed
an invalid completion gate: the server rejected every receipt acknowledgement
after trying to find the routed requester in its local `Player` scene objects,
so the completed batch and global lease remained occupied until disconnect.

The current source keeps owner receipts and routed-sender correlation, makes
exact settlement the completion boundary, and reduces receipt removal to
one-way cleanup. It also moves the lease to generation `v2` and the transaction
protocol to generation `v4`. Put Away stops before scanning or reservation
unless every connected peer announces the current Put Away generation. The
server also validates the recorded connected-peer cohort before each container
reservation. A join, disconnect, or readiness change stops the batch before its
next reservation. The lease remains with the holder until the holder releases
it. The protocol catches diagnostic failures before they can interrupt the
transaction.

After each existing cohort validation, the requester can start another
independent owner-authoritative chest transaction before earlier transactions
settle. This changes only requester-side batch scheduling. The lease generation
remains `v2`, the transaction generation remains `v4`, and no wire payload
shape changes. The batch becomes terminal and Put Away releases the global
lease only after scheduling stops and all in-flight deposits settle. Automated
checks pass for out-of-order results, partial refunds, validation failures,
thrown callbacks, duplicate settlement, and draining before lease release. The
scheduler still needs live multiplayer latency and durability proof.

When the requester disconnects, the server removes that requester's pending and
completed route entries. Automated stale-payload,
lease, mixed-version, receipt, throwing-sink, disconnect-cleanup, and real
Valheim package/routed-identity checks are green. These corrections are not
deployed and are not gameplay-proven. They still need the authorized
multiplayer test before they can be called fixed:

- A and B contend for the lease; the loser moves nothing.
- A deposits, then B deposits from a deliberately stale equal-revision cache.
- The owner and both clients show base + A + B with exact conservation.
- Reverse requester and chest-owner roles, test partial capacity, and delay one
  response to exercise the connected correlated retry.
- Transfer ownership after the owner applies and records its receipt but before
  the server accepts the result. Delay or lose that result, retry while still
  connected, and prove exact-once application plus peer convergence.
- After exact settlement, prove a lost or rejected receipt cleanup cannot keep
  the Put Away batch or global lease occupied. A second Put Away must start
  without reconnecting.

No source test proves live ZDO replication. Captured gameplay logs are test
evidence and are not committed.

## Diagnostic Lifecycle

The client emits `InventoryTransaction/put_away_batch_started` with
`operation_phase=start` and `status=running`. It emits
`InventoryTransaction/put_away_batch_finished` with
`operation_phase=terminal`, `status=completed|cancelled`, a reason, and the same
`operation_id`. These are the canonical batch lifecycle events.

Lease request, result, entry, and release events remain in the `Inventory`
domain with `lease_request`, `lease_result`, `mutation_allowed`, and
`lease_release` phases. They are not batch terminals. Each requester deposit
uses the batch `operation_id` and a transaction `correlation`. The server router
and chest owner use that same correlation because they do not receive the
client-only batch ID.

Transaction events preserve the complete evidence deliberately constructed by
each protocol role, including chest and peer IDs, revisions, contents, item
counts, positions, attempts, status, and reasons. Client-hosted roles use the
existing readable log, local NDJSON, and direct-client diagnostic path. The
dedicated server writes the same typed fields to readable diagnostics. The
protocol sends each result and completes each settlement or callback before it
writes the related diagnostic event. A diagnostic sink failure cannot change
transaction progress.

`client_result` means the requester completed exact settlement from a result
forwarded by the latest owner the server resolved. The requester runs each Put
Away callback after its correlated transaction settles. It emits the batch
terminal only after scheduling stops and every in-flight transaction settles.
Neither action waits for receipt cleanup.
`client_receipt_ack_sent` records that single cleanup send;
`owner_receipt_acknowledged` is an owner-local removal event, not a response to
the requester. Neither event is a commit or completion boundary. Put Away does
not force, retry, or gate on a character save and makes no disk-persistence
claim.

Put Away records exactly five bounded duration fields on existing terminal
events. The batch terminal event includes `batch_duration_ms` for the whole
batch and `scan_match_duration_ms` for the requester's aggregate scan-and-match
time. Each settled transaction's terminal event includes
`routing_owner_handoff_duration_ms` for routing and owner handoff and
`requester_settlement_duration_ms` for settlement, including any refund. The
correlated current-owner result event includes `owner_mutation_duration_ms` for
owner mutation.

These durations use a monotonic clock and show where elapsed time accumulated.
They do not affect protocol decisions, add recovery paths, or gate transaction
progress.

## Supported Failures and Non-Goals

The protocol fails closed when the server, owner, requester, access check,
transaction identity, receipt capacity, or compatible protocol is unavailable.
Put Away stops before scanning or reservation if any connected peer has not
announced the current Put Away generation. A connected-peer cohort change after
lease grant stops the batch before its next container reservation. Deposits
already in flight still settle before the batch terminal and lease release.
An ambiguous reserved request remains correlated and retryable while the
session stays connected instead of being locally restored as if the owner had
not committed.

Receipt cleanup is best effort after completion. A lost cleanup can leave one
bounded owner receipt behind; a full ledger then fails closed and reduces Put
Away availability. It cannot lose or duplicate items, repeat settlement, or
retain the completed batch and lease.

Crash and reconnect recovery during an in-flight reservation are unsupported.
When a requester disconnects, the server removes that requester's pending and
completed route entries.
Persistent journals and audit trails, legacy-protocol upgrades, and a general
capability platform are deferred. The removed historical platform included
those paths, but they never became proven Current Behavior. Ben explicitly
deferred them as over-optimization relative to Valheim's native contract. A
connected-session retry receipt does not make that process boundary durable.

This protocol owns Put Away deposits only. It does not change manual chest use,
create a general inventory transaction framework, or make the server an
inventory authority. Refactoring is welcome when the same regression controls
and durability contract prove equivalence.
