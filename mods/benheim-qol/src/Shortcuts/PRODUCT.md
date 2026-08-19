# Shortcuts

The Shortcuts module gives players one Valheim-styled Benheim menu. Players can
use it to find controls and passive features.

## Current Behavior

- `Left Shift + B` shows or hides the menu unless the player is typing.
  `Escape` and the native Close button also hide it.
- The dimmed, Valheim-styled menu uses Controls and Features tabs.
- The header shows the loaded Benheim version.
- The Controls tab uses aligned key and action columns grouped by Inventory,
  Crafting & Repair, and Farming.
- The Features tab explains extended reach, faster portal transitions,
  Rockbreaker, Cleave, global Bow-arrow headshots, adrenaline feedback,
  CLUTCH, UNTOUCHABLE, BERSERKER, SLAUGHTERHOUSE, and diagnostic export. The
  combat entries display their current triggers and bonuses. The BERSERKER and
  SLAUGHTERHOUSE entries identify that they require Server Support. These
  entries add no gameplay toggles.
- The menu explains manual pocketing and automatic protection for equipped and
  hotbar items.
- The menu lists `Left Shift` actions for production inputs, cooking slots,
  fuel, harvesting, and planting.
- The menu lists Wood Cutting cleave as a passive skill feature.
- `F7` copies the active Benheim diagnostic log to the player's Desktop with a
  timestamped filename on Mac and Windows.
- The game shows the exported filename so the player can find and attach the
  diagnostic log when reporting a problem.
- With Valheim's native console setting enabled, Ben confirmed that `/` opens
  the native console during normal gameplay.
- Ben confirmed that the `0.1.48` menu presentation looks good.

## In Development

- The `/` shortcut never closes or toggles the console. F5 and Escape keep
  their native behavior. The shortcut does nothing while the player is typing
  or while a menu, password field, or other text input is active. This
  suppression still needs gameplay or diagnostic proof.
- The Controls tab lists plain `R` for Loadout Swap and explains that it
  replaces Hide weapons when both loadouts are available.
- When a Benheim shortcut overlaps a different currently configured Valheim
  action, the Controls tab shows a compact amber Warnings block that names the
  Benheim action and key, plus the conflicting native action. Loadout Swap
  sharing `R` with Hide weapons is intentional and does not produce a warning;
  another native action on `R` does.
- The warning block stays hidden when there are no actual conflicts.
- If Benheim disables its gameplay actions because a required hook did not
  load, the Warnings block keeps that failure visible. If native keybind
  inspection fails, the block explains that collision warnings are unavailable
  without interrupting unrelated gameplay.
- The menu says that **Place stacks**, **Hold to stack**, and Put Away keep
  manually pocketed, equipped, and hotbar items with the player.
- The headshot description must stay aligned with the Archery module's proven
  behavior and remain explicit about collision-time feedback and native
  WeakSpot handling.
- Configured private-test builds show one notice before remote forwarding
  starts. The Config tab shows a persistent **Share Diagnostics** toggle, and
  sharing starts enabled. A one-time migration enables sharing for legacy
  private-test configurations that still use the earlier disabled default.
  After the migration, turning off **Share Diagnostics** persists the choice
  and stops remote forwarding immediately. Public and unconfigured builds
  receive no remote credentials. Local diagnostics, including
  `BenheimEvents.ndjson`, remain enabled.
- Extended reach, Rockbreaker, and Cleave descriptions must match their current
  ranges, unlocks, and target behavior.

## Later

- Let players configure Benheim shortcut keys. Do not add a general binding
  framework until a feature needs it.
