# Inventory

The Inventory module makes routine item movement faster.

## Current Behavior

- You can immediately enter a number in a split-stack dialog.
- `Backspace` or `Delete` resets the amount to `1`.
- When a container is open, `Enter` moves the split stack between the player
  inventory and the container.
- Press `P` while hovering over an item to toggle manual pocketing. Pocketed
  items show a `P` marker.

## In Development

- `Left Shift` + `P` moves matching items into eligible chests within 30 meters.
  It works during normal gameplay and while the inventory is open.
- In multiplayer, the server approves each deposit and routes it to the game
  instance that currently owns the destination chest. Only that instance
  changes the chest inventory.
- Put Away works only when the server and every connected player use the same
  transaction protocol. Otherwise, it moves nothing and explains the mismatch.
- Benheim retries a delayed request with the same transaction ID. Each chest
  records a limited history of recent transaction IDs so a retry cannot deposit
  items twice.
- Before removing an item from the player, Benheim records the pending transfer
  locally. After an interrupted session, it rolls back a transfer the server
  never saw or retries the same transaction after a transfer was reserved.
- If a chest accepts only part of a stack, Benheim returns the rejected amount
  to the player. Put Away must never claim chest ownership or change a copy of
  the chest inventory that other players cannot see.
- An eligible chest already contains the item and has room for more.
- Put Away checks eligible chests from nearest to farthest.
- Put Away reports the quantity and name of each item type moved.
- Manually pocketed items stay with the player during Put Away.
- Show one result line for each destination chest. Sort the items on each line
  by moved quantity, from highest to lowest. Identify the chest by its distance
  and compass direction from the player when Put Away finishes.
- When Put Away starts with the inventory closed, show a short generic summary
  above the player for 3 seconds. On success, show `Put away N items`, where `N`
  is the number of units moved. Show `Put away 1 item` for one unit. Show
  `Nothing to put away` when no units move. Do not show floating receipts above
  destination chests.
- When Put Away starts with the inventory closed, show its detailed result in
  a dedicated top-left receipt. Match Valheim's native message styling without
  moving or replacing Valheim's own message feed. Start the receipt below the
  visible hotbar slots and extend additional lines downward.
- When Put Away starts with the inventory open, show the detailed result in
  Valheim's center message area so the inventory cannot cover it. Show no
  above-player summary.
- Show pocket and unpocket confirmations only in the normal top-left message
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

## Test Gate

- Start with low-value stacks and confirm one normal Put Away transaction before
  testing valuable materials.
- Confirm that two players see each deposit immediately, regardless of which
  player opened the chest last.
- Repeat the same deposit into one chest and confirm that no items duplicate or
  disappear.
- Connect one client without the matching protocol and confirm that Put Away
  disables itself without affecting normal chest use.
