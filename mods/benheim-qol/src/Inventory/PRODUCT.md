# Inventory

The Inventory module makes routine item movement faster while keeping the
player in control of what stays in their inventory.

## Behavior

- Split-stack dialogs accept immediate numeric input.
- `Backspace` or `Delete` resets the amount to `1`.
- When a container is open, `Enter` moves the split stack between the player
  inventory and the container.
- Equipped items, hotbar items, and manually pocketed item types stay with the
  player during quick stack.
- Hover + `P` or `Left Alt` + click toggles manual pocketing.
- `Left Alt` + `P` quick-stacks items into nearby chests that already contain a
  matching item.

## Status

- **Tested:** Split-stack input and transfer work well.
- **In development:** Quick stack does not currently move eligible items.
- **Needs test:** Pocket markers, saved pocket choices after relaunch, and
  protection for equipped, hotbar, and pocketed items during quick stack.
