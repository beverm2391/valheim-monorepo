# Inventory

The Inventory module makes routine item movement faster.

## Durability Decision

Put Away prioritizes item integrity over implementation size. Multiple simpler
multiplayer implementations produced live stale writes and item loss. Benheim
may use a substantial owner-authoritative protocol when that complexity is
necessary to prevent loss, duplication, or invisible writes. Do not remove
protocol machinery only to reduce code size. A simplification must preserve
the same durability contract and pass the stale-payload regression proof.

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
- When Put Away starts with the inventory closed, it also shows a grouped
  receipt. The receipt matches Valheim's native message styling without moving
  or replacing Valheim's native message feed.
- When Put Away starts with the inventory open, its detailed result appears in
  Valheim's center message area so the inventory cannot cover it. No
  above-player summary appears.
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
- Before Put Away scans chests, Benheim Server Support grants one global lease
  to one connected player. Another simultaneous Put Away stops before any
  chest or inventory mutation and says `Put Away busy — retry in a few
  seconds`.
- Put Away routes each deposit to the current chest owner through the
  [owner-authoritative
  protocol](../../../../shared/benheim-inventory-protocol/PROTOCOL.md). The
  protocol owns the transaction mechanics and stale-payload controls.
- Every connected player sees the same completed chest state, including after
  chest ownership changes.
- Each accepted item moves once. Rejected items return to the requester's
  inventory. If the inventory cannot accept a rejected remainder, Put Away
  drops that exact remainder nearby and shows `Put Away refund dropped nearby.
  Pick it up.`
- Put Away reports success after the requester settles every accepted,
  refunded, or emergency-dropped item from the correlated owner result. Its
  one-way receipt cleanup does not retain the completed batch or global lease.
  Put Away does not force or gate on a character save, and Valheim's native
  character and world save lifecycle remains unchanged.
- Put Away emits complete typed transaction evidence. Diagnostic failures
  cannot block settlement, result delivery, batch completion, or lease
  release.
- Ben's three-player `0.1.66` review accepted the owner-authoritative path. The
  review covered rapid repeated use, both assignments of requester and current
  chest owner, simultaneous contention followed by immediate reuse, and exact
  accepted/refunded settlement. Players saw no item loss, duplication, stuck
  batch or global lease, or chest-state disagreement.
- Crash or reconnect recovery during an in-flight reservation is unsupported.

## In Development

- Plain `R` swaps between the items in hotbar slots `1` and `2` equipped
  together and the item in slot `3` equipped alone.
- Loadout swap uses Valheim's normal equip and unequip actions. It does nothing
  during text entry or other UI states that block gameplay shortcuts.
- `R` keeps Valheim's normal Hide weapons behavior unless slots `1` and `2`
  can form an equipable paired loadout and slot `3` can form an equipable
  single-item loadout.
- Live `0.1.67` timing showed that owner routing and handoff dominated
  multi-chest Put Away latency. After each existing cohort validation, Put Away
  can start another owner-authoritative chest deposit before earlier deposits
  settle when their item names do not overlap. If a later eligible chest
  contains the same item name, Put Away waits for exact settlement and scans
  that chest after any refund returns. Results for independent deposits can
  settle in any order. A validation failure stops new reservations. The batch
  becomes terminal and Put Away releases the global lease only after scheduling
  stops and every reserved deposit settles. This scheduler still needs live
  multiplayer latency and durability proof.
- Put Away's grouped receipt keeps every destination line and every item line.
  Its placement follows the shared top-left feedback lane defined in the root
  product document and still needs gameplay proof.
- Pocket and unpocket confirmations and Put Away's already-in-progress message
  use that same lane. They never enter Valheim's native top-left status-message
  feed.
- Valheim's **Place stacks** button and **Hold to stack** action must keep
  manually pocketed, equipped, and hotbar items in the player's inventory.
  Manual item moves and **Take all** must remain unchanged.
- For an active Put Away batch, the displayed result must include only items
  moved into the chest currently being processed.
