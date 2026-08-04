# Inventory

The Inventory module makes routine item movement faster.

## Current Behavior

- You can immediately enter a number in a split-stack dialog.
- `Backspace` or `Delete` resets the amount to `1`.
- When a container is open, `Enter` moves the split stack between the player
  inventory and the container.
- Press `P` while hovering over an item to toggle manual pocketing.
- Put Away treats a chest as eligible only when it already contains the item
  and has room for more.
- `Left Shift` + `P` moves matching items into eligible chests within 30 meters.
  It works during normal gameplay and while the inventory is open.
- Put Away does not run while the player edits a portal tag, a map pin name, or
  another text field.
- In multiplayer, the server routes each deposit to the game instance that
  currently owns the destination chest. Only that instance validates and
  changes the chest inventory.
- Two players see each successful deposit immediately, including when the other
  player's game instance owns the destination chest.
- Put Away checks eligible chests from nearest to farthest.
- Put Away reports the quantity and name of each item type moved.
- Manually pocketed items stay with the player during Put Away.
- The detailed result shows one line for each destination chest. Each line
  sorts items by moved quantity, from highest to lowest. It identifies the
  chest by its distance and compass direction when Put Away finishes.
- When Put Away starts with the inventory closed, it shows a short summary above
  the player for 3 seconds. The summary says `Put away N items`, `Put away 1
  item`, or `Nothing to put away`. No receipt appears above a destination chest.
- When Put Away starts with the inventory closed, it also shows a dedicated
  top-left receipt. The receipt matches Valheim's native message styling
  without moving or replacing Valheim's message feed. It starts below the
  visible hotbar slots, and additional lines extend downward.
- When Put Away starts with the inventory open, its detailed result appears in
  Valheim's center message area so the inventory cannot cover it. No
  above-player summary appears.
- Pocket and unpocket confirmations appear only in the normal top-left message
  feed.
- A gold `P` in the top-left of an item slot marks manual pocketing, which the
  player can toggle.
- Pocketing a stackable item protects every stack of that item type. Pocketing
  a non-stackable item protects only that exact item.
- Manual pocketing persists after the game relaunches. For a non-stackable
  item, protection stays with the marked item when it moves.
- Equipped and hotbar items remain automatically protected without showing a
  marker. If an item is also manually pocketed, hide its `P` while automatic
  protection applies and show the same manual `P` again when it no longer
  applies.
- Hold `Left Alt` while clicking an item to toggle manual pocketing.

## In Development

- Put Away works only when the server and every connected player use the exact
  transaction protocol version. Otherwise, it moves nothing and explains the
  mismatch.
- The transaction safety contract in
  `shared/benheim-inventory-protocol/PROTOCOL.md` remains in development. It
  owns retry identity, duplicate prevention, reservations, item restoration,
  chest ownership, and recovery.
- A delayed, retried, interrupted, or partially accepted transfer must not lose
  or duplicate items. Put Away returns every amount that a chest rejects.
- Put Away must never produce a chest state that another player cannot see.

## Test Gate

- Repeat the same deposit into one chest and confirm that no items duplicate or
  disappear.
- Interrupt a deposit before its response and confirm reconnect recovery neither
  loses nor duplicates the reserved items.
- Fill a matching chest almost completely and confirm that Put Away returns any
  amount the chest cannot accept.
- Connect one client without the matching protocol version and confirm that Put
  Away disables itself without affecting normal chest use.
