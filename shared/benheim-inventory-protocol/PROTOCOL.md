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
- The accepted result restores every rejected item and no accepted item.
- Chest contents plus requester contents conserve the exact item counts.
- Connected peers converge on the owner's committed chest contents.
- An ambiguous response remains pending and correlated while the session stays
  connected. It never becomes an uncorrelated local retry.

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
6. The requester accepts only a matching transaction ID and payload hash. It
   restores rejected remainders, saves the character, and acknowledges the
   receipt.
7. The owner removes the receipt after the acknowledgement. The lease is
   released when the Put Away batch finishes.

## Traceability

| Protocol component | Invariant protected | Concrete failure if removed | Proving test |
| --- | --- | --- | --- |
| Global server lease | At most one Benheim Put Away batch enters scanning and mutation | Two requesters mutate replicated chest snapshots concurrently | `Put Away lease exclusion checks passed` |
| Immutable transaction ID plus payload hash | Results and retries attach only to their request | A stale or conflicting result refunds or commits the wrong reservation | `Put Away owner-authoritative stale-payload integration checks passed` |
| Server routes to the current owner; requester accepts a result only from the latest owner the server resolved | Only authoritative contents are read and changed | A requester overwrites a newer chest with its stale cache, or a delayed old-owner result settles after rerouting | `Put Away owner-authoritative stale-payload integration checks passed` |
| Owner receipt and server completion cache | A retry applies at most once across routing and ownership changes | Lost responses duplicate accepted items | receipt codec test plus the connected-retry integration case |
| Accepted counts and requester remainder restoration | Player plus chest item counts are conserved | Partial capacity loses rejected items or duplicates accepted items | stale-payload and partial-capacity integration cases |
| Character save before receipt acknowledgement | A completed connected transfer persists the source removal before its owner receipt is cleared | Clearing the receipt first leaves a larger duplicate gap | source ordering guard and multiplayer interruption test |

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

The current adaptation uses the same owner-authority and correlation model
inside Benheim Server Support. Automated stale-payload, lease, and receipt
checks are green. The adaptation is not deployed and is not gameplay-proven.
It still needs the authorized multiplayer test before it can be called fixed:

- A and B contend for the lease; the loser moves nothing.
- A deposits, then B deposits from a deliberately stale equal-revision cache.
- The owner and both clients show base + A + B with exact conservation.
- Reverse requester and chest-owner roles, test partial capacity, and delay one
  response to exercise the connected correlated retry.

No source test proves live ZDO replication. Private gameplay logs remain local.

## Supported Failures and Non-Goals

The protocol fails closed when the server, owner, requester, access check,
transaction identity, receipt capacity, or compatible protocol is unavailable.
An ambiguous reserved request remains correlated and retryable while the
session stays connected instead of being locally restored as if the owner had
not committed.

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
