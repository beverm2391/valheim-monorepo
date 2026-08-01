# Inventory

The Inventory module makes routine item movement faster.

## Current Behavior

- You can immediately enter a number in a split-stack dialog.
- `Backspace` or `Delete` resets the amount to `1`.
- When a container is open, `Enter` moves the split stack between the player
  inventory and the container.

## In Development

- Quick stack does not currently move any items and needs debugging.
- When it works, `Left Alt` + `P` will quick-stack items into nearby chests that
  already contain a matching item.
- Quick stack will keep equipped items, hotbar items, and all items of manually
  pocketed types with the player.
- Press `P` while hovering over an item to toggle manual pocketing for that item
  type.
- Hold `Left Alt` while clicking an item to toggle manual pocketing for that
  item type.
- Items of pocketed types will show a marker. The set of pocketed item types
  will persist after the game relaunches.
