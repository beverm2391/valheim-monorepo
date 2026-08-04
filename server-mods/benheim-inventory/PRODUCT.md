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

- Put Away works only when the server and every connected player use the same
  protocol version.
- A missing or mismatched client disables Put Away for everyone. It does not
  disconnect that player.
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
