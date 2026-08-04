# Benheim Inventory

Benheim Inventory coordinates multiplayer Put Away without changing normal
chest use. Valheim assigns each chest to one game instance at a time. That game
instance is the current chest owner. The server routes each transaction, and
the current chest owner validates and performs the deposit.

## Current Behavior

- The server plugin loads through the existing modded server path.
- Vanilla clients can still join and use chests normally.
- The server and matching clients report transaction protocol readiness.
- The server routes each deposit to the current chest owner. Before depositing,
  the recipient confirms that it still owns the chest. It also checks access,
  player distance, and whether the chest already contains that item.
- Two players see each successful deposit immediately, including when the other
  player's game instance owns the destination chest.

## In Development

- Benheim Inventory `0.1.2` and transaction protocol `2` are the next test
  versions. They have not passed gameplay proof.
- Each ready client reports its transaction protocol and exact Benheim version.
  The server publishes its own version and protocol, plus each ready player's
  detected version, protocol, and compatibility state. The
  [Shortcuts product](../../mods/benheim-qol/src/Shortcuts/PRODUCT.md) owns its
  client presentation.
- Put Away works only when the server and every ready player use the same
  transaction protocol. Exact semantic versions do not decide compatibility.
- A missing or mismatched client disables Put Away for everyone. It does not
  disconnect that player or change normal chest use.
- The retry and recovery contract in
  `shared/benheim-inventory-protocol/PROTOCOL.md` remains in development. It
  owns transaction identity, duplicate prevention, chest receipts,
  reservations, and recovery across ownership changes.
- Retried or interrupted transfers must not lose or duplicate items, including
  after chest ownership changes.
- Removing the plugin leaves normal chest contents intact. Benheim's recent
  transaction records may remain in the world save, but Valheim ignores them
  after the plugin is removed.

## Test Gate

- Repeat deposits and ownership changes without losing or duplicating items.
- Interrupt a transaction after the player reserves items and confirm reconnect
  recovery neither loses nor duplicates them.
- Confirm a nearly full chest accepts only what fits and returns the remainder.
- Join with a missing or mismatched client and confirm Put Away disables itself.
