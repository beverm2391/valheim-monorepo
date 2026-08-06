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

- Plain `R` swaps between the items in hotbar slots `1` and `2` equipped
  together and the item in slot `3` equipped alone.
- Loadout swap uses Valheim's normal equip and unequip actions. It does nothing
  during text entry or other UI states that block gameplay shortcuts.
- `R` keeps Valheim's normal Hide weapons behavior unless slots `1` and `2`
  can form an equipable paired loadout and slot `3` can form an equipable
  single-item loadout.
- Put Away can cause a brief frame hitch when it scans many nearby chests and
  matches their contents against the player's inventory.
- Put Away asks the current owner for access to each chest through Valheim's
  Stack All action. It moves items only after the owner grants access.
- During normal gameplay, each completed transfer must move each accepted item
  once and leave each rejected remainder in the player's inventory. Every
  connected player must see the same chest state, including after chest
  ownership changes.
- Put Away keeps native inventory persistence and interruption behavior. It
  does not force a character save or add a transfer journal, transaction
  receipt, retry, or crash recovery.
- The transfer must work with either player as the requester or current chest
  owner, and a completed transfer must remain visible after ownership changes.
- Valheim's **Place stacks** button and **Hold to stack** action must keep
  manually pocketed, equipped, and hotbar items in the player's inventory.
  Manual item moves and **Take all** must remain unchanged.
- For an active Put Away batch, the displayed result must include only items
  moved into the chest currently being processed.
- Put Away should continue to work while a player without Benheim is online.
  This is useful compatibility evidence, but it is not a release gate.
