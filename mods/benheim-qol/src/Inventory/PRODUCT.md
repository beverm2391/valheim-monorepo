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
- Quick stack reports the quantity and name of each item type moved.
- Manually pocketed item types stay with the player during quick stack.
- Press `P` while hovering over an item to toggle manual pocketing for that item
  type. Pocketed items show a `P` marker.

## In Development

- Show one result line for each destination chest. Sort the items on each line
  by moved quantity, from highest to lowest. Identify the chest by its distance
  and compass direction from the player when Put Away finishes.
- Briefly show the item names and quantities received above each destination
  chest so the player can locate it in the world.
- Put Away always keeps its existing detailed result in the normal top-left
  message feed.
- When Put Away starts with the inventory closed, also show a short summary
  above the player. On success, show `Put away N items`, where `N` is the total
  number of units moved. Show `Put away 1 item` for one unit and `Nothing to put
  away` when no units move.
- When Put Away starts with the inventory open, show no above-player summary.
- Show pocket and unpocket confirmations only in the normal top-left message
  feed.
- A gold `P` marks manual pocketing, which the player can toggle. A cyan `P`
  marks automatic protection that lasts while an item is equipped or in the
  hotbar. Both markers appear in the bottom-left of the item slot.
- Confirm that equipped items and hotbar items stay with the player during quick
  stack.
- Hold `Left Alt` while clicking an item to toggle manual pocketing for that
  item type.
- Confirm that the set of pocketed item types persists after the game relaunches.
