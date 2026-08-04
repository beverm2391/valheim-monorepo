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
   available only when the server and every ready player match.
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

## Capability And Roster Status

Transaction protocol `2` adds versioned capability and roster status. It is the
next test protocol and has not passed gameplay proof. The next test client is
Benheim `0.1.41`. The next test server plugin is Benheim Inventory `0.1.2`.

Each ready client sends a capability hello that contains its transaction
protocol and exact Benheim version. The server records that hello for the
client's current peer connection. Missing capability data means Benheim was not
detected for that connection.

The server sends each client a status snapshot with:

- the server plugin version and transaction protocol;
- the server's current Put Away readiness;
- each ready player's name;
- each ready player's reported Benheim version and protocol;
- whether Benheim was detected for that player; and
- whether that player's transaction protocol is compatible.

Compatibility depends only on the transaction protocol. Semantic versions are
diagnostic information. Two builds with different semantic versions remain
compatible when they use the same transaction protocol.

Put Away is ready only when the server and every ready player use the same
transaction protocol. A missing or mismatched client disables Put Away for all
players. It does not reject that client, disconnect anyone, or change normal
gameplay and chest use.

The client uses this snapshot for compatibility feedback.
`mods/benheim-qol/src/Shortcuts/PRODUCT.md` owns roster presentation.
`mods/benheim-qol/src/Inventory/PRODUCT.md` owns warning behavior.

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

Protocol `2` can recover journal requests written by protocol `1`. A client that
uses protocol `2` sends the original protocol `1` request bytes through the
protocol `2` remote procedure call (RPC). The server and chest owner parse the
protocol `1` request and return a protocol `2` response. They do not rewrite the
request or its protocol field.

This rule preserves the original payload hash. A `Reserved` retry can therefore
find a receipt left by a protocol-1 owner and return its recorded result without
changing the chest again. If no receipt exists, the owner applies the original
request once and records its result. If a request version is unsupported or a
record's fields do not match its phase, Benheim leaves the record in the
journal, blocks recovery, and logs a warning. Recovery does not delete or
reinterpret that record.

A receipt acknowledgement includes the original request bytes. The server
validates the request's payload hash, transaction ID, chest ID, and player ID
before routing the acknowledgement. This proof lets the client acknowledge the
receipt for a recovered `Completed` journal after the server restarts and loses
its in-memory completion record. The chest owner can then clear the receipt.
The client does not retry the deposit while recovering a `Completed` journal.

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

Each client and the server keep a limited Put Away audit across relaunches. The
audit uses two files: the current `BenheimInventoryAudit.log` and a previous
audit file. Audit entries record transaction phases, item amounts, retries,
recovery, and warnings. The audit adds a capability entry only when Put Away
readiness changes. The client `F7` export includes both audit files.

The two-player owner-routed deposit path described above is proven. Retry
deduplication, interrupted recovery, partial capacity, and mismatched-client
handling still require limited, task-scoped gameplay tests. The client and
server `PRODUCT.md` files own those remaining product gates.
