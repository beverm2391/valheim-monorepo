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
- Exact requester settlement completes the deposit. Receipt cleanup cannot
  retain the Put Away batch or global lease.

## Flow

1. Benheim Server Support grants the requester the global Put Away lease before
   scanning starts.
2. The requester selects a chest, creates an immutable transaction ID, chest
   ID, request payload, and payload hash, then reserves the selected source
   stacks in memory before sending the request.
3. The server deduplicates the transaction, resolves the chest's current owner,
   and routes the request to that peer.
4. The owner confirms ownership and validates the requester, access, distance,
   item match, chest use, and live capacity.
5. The owner applies accepted items to its inventory, records a receipt on the
   chest ZDO, and returns accepted counts through the server.
6. The server accepts a result only from the latest owner it resolved. The
   requester then requires a matching transaction ID and payload hash. It
   settles every accepted, refunded, or emergency-dropped item exactly once.
   Settlement completes the deposit so the batch can continue and eventually
   release the lease.
7. After completion, the requester sends exactly one best-effort, one-way
   receipt cleanup request. It keeps no cleanup state and does not retry. The
   server requires the completed correlation and original routed sender, then
   forwards the request to the chest's current owner. The owner sends no
   cleanup confirmation through the server. A lost or rejected cleanup does
   not repeat settlement, reopen completion, delay callbacks, or retain the
   batch or lease.

## Traceability

| Protocol component | Invariant protected | Concrete failure if removed | Proving test |
| --- | --- | --- | --- |
| Global server lease | At most one Benheim Put Away batch enters scanning and mutation | Two requesters mutate replicated chest snapshots concurrently | `Put Away lease exclusion checks passed` |
| Immutable transaction ID plus payload hash | Results and retries attach only to their request | A stale or conflicting result refunds or commits the wrong reservation | `Put Away owner-authoritative stale-payload integration checks passed` |
| Server routes to the current owner and accepts only the latest resolved owner's result; requester validates the transaction ID and payload hash | Only authoritative contents are read and changed | A requester overwrites a newer chest with its stale cache, or a delayed old-owner result settles after rerouting | `Put Away owner-authoritative stale-payload integration checks passed` |
| Owner receipt and server completion cache | A retry applies at most once across routing and ownership changes | Lost responses duplicate accepted items | receipt codec test plus the connected-retry integration case |
| Accepted counts and requester remainder restoration | Player, chest, and explicit refund-drop item counts are conserved | Partial capacity loses rejected items or duplicates accepted items | stale-payload, partial-capacity, and filled-inventory refund cases |
| Exact settlement before completion | Completion reflects every accepted, refunded, or emergency-dropped item | Early completion hides an unsettled remainder and can lose or duplicate items | source ordering guard, filled-inventory refund case, and receipt-acknowledgement wire/liveness case |
| One-way current-owner receipt cleanup | Completed receipt entries are normally reclaimed without becoming a transaction liveness gate | Removing cleanup can eventually exhaust receipt capacity; confirmed cleanup adds state and failure modes without protecting item integrity | receipt-acknowledgement wire/liveness case |
| Complete typed transaction events | Requester, router, and owner decisions can be correlated across peers with the exact chest state that informed them | A runtime failure appears successful or cannot be distinguished from stale state | `inventory transaction typed diagnostic schema checks passed` |

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
one-way cleanup. Automated stale-payload, lease, receipt, and real Valheim
package/routed-identity checks are green. This correction is not deployed and
is not gameplay-proven. It still needs the authorized multiplayer test before
it can be called fixed:

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
dedicated server writes the same typed fields to readable diagnostics.

`client_result` means the requester completed exact settlement from a result
forwarded by the latest owner the server resolved. Final Put Away callbacks and
terminal batch events follow settlement without waiting for receipt cleanup.
`client_receipt_ack_sent` records that single cleanup send;
`owner_receipt_acknowledged` is an owner-local removal event, not a response to
the requester. Neither event is a commit or completion boundary. Put Away does
not force, retry, or gate on a character save and makes no disk-persistence
claim.

## Supported Failures and Non-Goals

The protocol fails closed when the server, owner, requester, access check,
transaction identity, receipt capacity, or compatible protocol is unavailable.
An ambiguous reserved request remains correlated and retryable while the
session stays connected instead of being locally restored as if the owner had
not committed.

Receipt cleanup is best effort after completion. A lost cleanup can leave one
bounded owner receipt behind; a full ledger then fails closed and reduces Put
Away availability. It cannot lose or duplicate items, repeat settlement, or
retain the completed batch and lease.

Crash and reconnect recovery during an in-flight reservation are unsupported.
Persistent journals and audit trails, legacy-protocol upgrades, and a general
capability platform are deferred. The removed historical platform included
those paths, but they never became proven Current Behavior. Ben explicitly
deferred them as over-optimization relative to Valheim's native contract. A
connected-session retry receipt does not make that process boundary durable.

This protocol owns Put Away deposits only. It does not change manual chest use,
create a general inventory transaction framework, or make the server an
inventory authority. Refactoring is welcome when the same regression controls
and durability contract prove equivalence.
