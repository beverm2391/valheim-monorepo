# Inventory

The Inventory module makes routine item movement faster.

## Current Behavior

- You can immediately enter a number in a split-stack dialog.
- `Backspace` or `Delete` resets the amount to `1`.
- When a container is open, `Enter` moves the split stack between the player
  inventory and the container.
- With the inventory open, `Left Alt` + `P` moves matching items into accessible
  nearby chests.
- An eligible chest already contains the item and has room for more.
- Manually pocketed item types stay with the player during quick stack.
- Press `P` while hovering over an item to toggle manual pocketing for that item
  type. Pocketed items show a `P` marker.

## In Development

- Confirm in gameplay that `Left Shift` + `P` activates quick stack during
  normal gameplay and while the inventory is open. It scans chests within 30
  meters of the player.
- Confirm in gameplay that quick stack shows the quantity and name of each item
  type moved.
- Confirm that equipped items and hotbar items stay with the player during quick
  stack.
- Hold `Left Alt` while clicking an item to toggle manual pocketing for that
  item type.
- Confirm that the set of pocketed item types persists after the game relaunches.
