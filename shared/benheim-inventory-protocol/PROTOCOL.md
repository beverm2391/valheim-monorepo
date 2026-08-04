# Put Away Transaction Protocol

A Valheim chest is replicated, but only one game instance owns its network
object at a time. Only that owner may authoritatively change the chest. Another
client can hold a local `Inventory` object for the same chest, but that object
is only a cache.

This ownership rule is the center of Benheim's Put Away design. Read this file
before changing chest writes, transaction retries, journals, receipts, or
recovery.

## Why Local Writes Failed

The first Put Away implementation found nearby `Container` objects and changed
their local inventories. A later attempt called `ZNetView.ClaimOwnership()`
before making the same local write.

Both approaches could look correct to the player who pressed Put Away. In each
approach, the requesting client showed the deposited item, and its local ZDO
revision changed. Another player could still see the old chest state. A later
authoritative update could then replace the local copy, which made deposited
items appear to disappear.

A successful local mutation does not prove that the authoritative chest state
changed. Claiming ownership is also not a synchronization shortcut. Put Away
must send the request to the current chest owner and let that owner use
Valheim's normal `Inventory` mutation path.

Git history preserves the failed implementations and their diagnostics. Use it
for forensics, but do not restore either design.

## Evidence Behind The Design

The current Valheim assemblies are the authority for game behavior. Inspect
them again after a game update. The relevant types include `Container`,
`Inventory`, `ZNetView`, `ZDO`, `ZDOMan`, `ZNetScene`, and `ZRoutedRpc`.

The owner-routed design was also informed by Multi User Chest at the pinned
commit recorded in `THIRD_PARTY_NOTICES.md`:

- [ContainerRPCHandler.cs](https://github.com/MSchmoecker/No-Chest-Block/blob/bf351eb4d66a0cfaa0847266bd92983a0a39bc63/MultiUserChest/ContainerRPCHandler.cs)
  resolves the chest instance and changes it only when that instance is owner.
- [QuickStackPatch.cs](https://github.com/MSchmoecker/No-Chest-Block/blob/bf351eb4d66a0cfaa0847266bd92983a0a39bc63/MultiUserChest/Patches/Compatibility/QuickStackPatch.cs)
  explicitly removes QuickStack's `ClaimOwnership` call.
- [Multi User Chest README](https://github.com/MSchmoecker/No-Chest-Block/blob/bf351eb4d66a0cfaa0847266bd92983a0a39bc63/README.md)
  explains the manager-routed request and response model.

`THIRD_PARTY_NOTICES.md` owns attribution and license text. This file owns the
Benheim-specific technical model.

## The Transaction

Put Away uses one client, the dedicated server, and the current chest owner.
The server coordinates the transfer but does not change the chest itself.

1. Every ready peer advertises the exact protocol version. Put Away becomes
   available only when the server and every connected player match.
2. The requesting client scans for eligible chests. It never writes a chest.
3. The client creates an immutable request. The request contains a transaction
   ID, player ID, chest ZDO ID, source positions, and serialized item snapshots.
   The client hashes the exact request bytes.
4. The client writes a `Prepared` journal record before removing any item.
5. The client removes the requested items from the player, writes the same
   journal as `Reserved`, and sends the original request bytes to the server.
6. The server deduplicates by transaction ID, requester, and payload hash. It
   resolves `ZDO.GetOwner()` and routes the exact request to that peer.
7. The current chest owner confirms that it still owns the chest. It checks the
   request, access, player identity, distance, chest use, item match, and
   capacity.
8. The current chest owner changes the chest through its normal `Inventory`.
   It then stores a receipt on the chest ZDO. After storing the receipt, it
   returns the accepted amounts.
9. The server caches and relays the response. The requesting client marks its
   journal `Completed`, restores each unaccepted amount, and shows the result.
10. The client saves the character profile. It then deletes the journal and
    acknowledges the receipt through the server.
11. The current chest owner removes the durable receipt after that
    acknowledgement.

The shared source under this directory compiles into both the client mod and
the server plugin. Keep one wire model and one protocol version.

## Duplicate Prevention

Every retry must reuse the original transaction ID and exact request bytes. A
new transaction ID would describe a second transfer, not a retry.

The server keeps pending and recently completed transactions in memory. The
chest also keeps a limited receipt ledger in its ZDO. The ledger follows the
chest when ownership changes. If an owner receives the same transaction again,
it returns the recorded result instead of changing the inventory again.

The receipt stays on the chest until the requesting client saves its character
profile and acknowledges the result. This order closes the dangerous gap where
the chest committed but the player's removal did not persist.

If ownership or protocol readiness changes after the client reserves items,
the server keeps the transaction pending. It must not tell the client to
restore items because the previous owner may already have committed the chest
write.

## Recovery

The local journal is scoped by world and character. Its phase determines the
only safe recovery action:

- `Prepared`: the request was recorded before item removal. Restore any missing
  source amount, then clean up after the character profile saves.
- `Reserved`: the player items were removed, but no final result was saved.
  Re-establish the reservation and resend the original transaction.
- `Completed`: the accepted amounts are known. Normalize the player inventory
  to those amounts, save the character, and acknowledge the chest receipt.

Recovery must preserve the request bytes, payload hash, transaction ID, player
ID, world ID, chest ID, source positions, and accepted amounts. Invalid journal
records do not become new transfers. Benheim does not invent a transfer from an
invalid journal record.

## Invariants

- Never change a chest from a peer that does not own its `ZNetView`.
- Never call `ClaimOwnership()` to make Put Away appear local.
- Never create a new transaction ID for a retry or recovered reservation.
- Never restore reserved items while the chest result is ambiguous.
- Never remove a chest receipt before the client saves and acknowledges.
- Never accept the same transaction ID with a different requester or payload.
- Never let a protocol mismatch kick a player or break normal chest use.
- Never add persistent item fields without updating and testing the wire model.

## How To Prove A Change

Run the inventory source checks, server inventory checks, and receipt codec
tests owned by the repo test suite. Then use a disposable two-player gameplay
test with low-value items:

1. Put a matching item in an isolated chest and make the other client its
   current owner.
2. Deposit from the first client. Both clients must see the same result
   immediately.
3. Reverse requester and owner, then repeat.
4. Read both client logs and the server journal. Correlate `client_sent`,
   `server_routed`, `owner_result`, `client_result`, `client_committed`, and
   `owner_receipt_ack` by transaction ID.

The two-player owner-routed deposit path described above is proven. Retry
deduplication, interrupted recovery, partial capacity, and mismatched-client
handling still require limited, task-scoped gameplay tests. The client and
server `PRODUCT.md` files own those remaining product gates.
