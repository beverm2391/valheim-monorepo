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
- Quick stack reports the quantity and name of each item type moved.
- Manually pocketed item types stay with the player during quick stack.
- Press `P` while hovering over an item to toggle manual pocketing for that item
  type. Pocketed items show a `P` marker.

## In Development

- Show one result line for each destination chest. Sort the items on each line
  by moved quantity, from highest to lowest.
- After a successful manual toggle, show `Pocketed` or `Unpocketed` above the
  player. Keep the item marker and detailed message.
- Show `Nothing to pocket` above the player when `P` is pressed without a
  hovered player-inventory item.
- A gold `P` marks manual pocketing, which the player can toggle. A cyan `P`
  marks automatic protection that lasts while an item is equipped or in the
  hotbar.
- Confirm that equipped items and hotbar items stay with the player during quick
  stack.
- Hold `Left Alt` while clicking an item to toggle manual pocketing for that
  item type.
- Confirm that the set of pocketed item types persists after the game relaunches.
