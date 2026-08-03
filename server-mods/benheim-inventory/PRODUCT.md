# Benheim Inventory

Benheim Inventory coordinates multiplayer Put Away without changing normal
chest use. Valheim assigns one game instance to manage each chest at a time;
this is the current chest owner. The server approves each transaction, and the
current chest owner performs the deposit through Valheim's normal save path.

## Current Behavior

- The server plugin loads through the existing modded server path.
- Vanilla clients can still join and use chests normally.

## In Development

- Put Away works only when the server and every connected player use the same
  protocol version.
- A missing or mismatched client disables Put Away for everyone. It does not
  disconnect that player.
- The server routes each deposit to the current chest owner. Before depositing,
  the recipient confirms that it still owns the chest. It also checks access,
  player distance, and whether the chest already contains that item.
- Requests retry with the same transaction ID. Each chest records a limited
  history of recent transactions. This history prevents a retry from depositing
  items twice, even after chest ownership changes.
- The client journals a transfer before removing items from the player. An
  interrupted pre-send reservation rolls back locally; an interrupted sent
  transaction retries with its original ID.
- Removing the plugin leaves normal chest contents intact. Benheim's recent
  transaction records may remain in the world save, but Valheim ignores them
  after the plugin is removed.

## Test Gate

- Verify the server and two clients report protocol readiness.
- Deposit into a chest last opened by each player and confirm both players see
  the result immediately.
- Repeat deposits and ownership changes without losing or duplicating items.
- Join with a missing or mismatched client and confirm Put Away disables itself.
