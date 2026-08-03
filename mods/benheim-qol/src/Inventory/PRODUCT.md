# Inventory

The Inventory module makes routine item movement faster.

## Current Behavior

- You can immediately enter a number in a split-stack dialog.
- `Backspace` or `Delete` resets the amount to `1`.
- When a container is open, `Enter` moves the split stack between the player
  inventory and the container.
- `Left Shift` + `P` moves matching items into accessible chests within 30
  meters. It works during normal gameplay and while the inventory is open.
- An eligible chest already contains the item and has room for more.
- Put Away checks eligible chests from nearest to farthest.
- Put Away reports the quantity and name of each item type moved.
- Manually pocketed items stay with the player during Put Away.
- Press `P` while hovering over an item to toggle manual pocketing. Pocketed
  items show a `P` marker.

## In Development

- Show one result line for each destination chest. Sort the items on each line
  by moved quantity, from highest to lowest. Identify the chest by its distance
  and compass direction from the player when Put Away finishes.
- When Put Away starts with the inventory closed, show a short generic summary
  above the player for 3 seconds. Do not show floating receipts above destination
  chests; the detailed HUD receipt already identifies each destination.
- When Put Away starts with the inventory closed, show its detailed result in
  a dedicated top-left receipt. Match Valheim's native message styling without
  moving or replacing Valheim's own message feed. Start the receipt below the
  visible hotbar slots and extend additional lines downward. On success, show
  `Put away N items`, where `N` is the total number of units moved. Show
  `Put away 1 item` for one unit and `Nothing to put away` when no units move.
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
- Confirm that equipped items and hotbar items stay with the player during Put
  Away.
- After Valheim grants access to a chest, Put Away must claim the chest before
  transferring items. Another player who opens the chest must see the
  transferred items.
- Hold `Left Alt` while clicking an item to toggle manual pocketing.
