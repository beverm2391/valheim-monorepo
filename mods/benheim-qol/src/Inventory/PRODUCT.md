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
- Ben confirmed the `0.1.49` solo Put Away behavior except for the dedicated
  receipt placement. The focused multiplayer gate remains unproven.

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
- Put Away's grouped receipt keeps every destination line and every item line.
  Its placement follows the shared top-left feedback lane defined in the root
  product document and still needs gameplay proof.
- Pocket and unpocket confirmations and Put Away's already-in-progress message
  use that same lane. They never enter Valheim's native top-left status-message
  feed.
- Put Away asks the current owner for access to each chest through Valheim's
  Stack All action. It moves items only after the owner grants access.
- Before Put Away scans chests, Benheim Server Support grants one global lease
  to one connected player. Another simultaneous Put Away stops before any
  chest or inventory mutation and says `Put Away busy — retry in a few
  seconds`.
- After Valheim grants a chest transfer, Put Away establishes requester-local
  ownership so the requesting client can perform the native chest write. It
  does this immediately before native **Stack All** changes the chest. Put Away
  rejects that chest if it cannot establish ownership.
- The write diagnostic records the chest's stable network identity, owner, data
  revision, moved items, resulting counts, and post-write contents. A separate
  snapshot records the first successful open of each chest during the other
  client's fresh test session. It captures the cached contents shown on that
  first open without waiting for a later replication refresh. The snapshot
  must match before another mutation. These diagnostics are evidence only.
  They do not retry or repair a failed write.
- During normal gameplay, each completed transfer must move each accepted item
  once and leave each rejected remainder in the player's inventory. Every
  connected player must see the same chest state, including after chest
  ownership changes.
- Put Away keeps native inventory persistence and interruption behavior. It
  does not force a character save or add a transfer journal, transaction
  receipt, automatic retry, or crash recovery.
- If a chest's native **Stack All** response does not arrive within 5 seconds,
  Put Away cancels the batch and releases the global lease. It does not retry
  the chest or continue to another chest in that batch. It tells the player to
  reconnect before retrying that chest.
- A timed-out chest stays unavailable to Put Away until one native **Stack
  All** response for that chest arrives or the client reconnects. Benheim
  discards exactly that one response. Later Put Away attempts can use other
  chests.
- Valheim does not identify Stack All responses by request. If the timed-out
  response never arrives, the next manual Stack All response for that chest can
  be the response that Benheim discards. This releases the chest for later
  attempts without moving items or creating a Put Away receipt.
- Automated lifecycle checks prove:
  - one lease winner;
  - contention rejection before scanning;
  - lease release when Put Away finishes, cancels, times out, or the lease
    holder disconnects;
  - timeout cancellation;
  - late-response discard; and
  - later use of other chests.
  The two-player race and timeout feedback still need gameplay proof.
- Ben previously confirmed that the earlier requester-local ownership and
  write-revision safeguard kept completed transfers visible to another player.
  Its restored form remains a candidate until the two-client visibility test
  proves the current implementation.
- The transfer must work with either player as the requester or current chest
  owner, and a completed transfer must remain visible after ownership changes.
- Valheim's **Place stacks** button and **Hold to stack** action must keep
  manually pocketed, equipped, and hotbar items in the player's inventory.
  Manual item moves and **Take all** must remain unchanged.
- For an active Put Away batch, the displayed result must include only items
  moved into the chest currently being processed.
