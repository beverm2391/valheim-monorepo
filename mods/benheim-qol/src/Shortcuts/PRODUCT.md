# Shortcuts

The Shortcuts module gives players one Valheim-styled Benheim menu. Players can
use it to find controls, passive features, and multiplayer compatibility
information.

## Current Behavior

- `Left Shift + B` shows or hides the menu unless the player is typing.
  `Escape` and the native Close button also hide it.
- The dimmed, Valheim-styled menu uses Controls, Features, and Multiplayer tabs.
- The header shows the loaded Benheim version and a compact Put Away status.
- The Controls tab uses aligned key and action columns grouped by Inventory,
  Crafting & Repair, and Farming.
- The Features tab explains extended reach, faster portal transitions,
  Rockbreaker, Cleave, adrenaline feedback, and diagnostic export.
- The Multiplayer tab shows the server and each player who has reported
  readiness. It includes their version, protocol, detection state, and Put Away
  compatibility.
- Transaction protocol versions determine compatibility. Exact semantic
  versions appear only for diagnosis.
- The menu explains manual pocketing and how Put Away automatically protects
  equipped items and hotbar items.
- The menu lists `Left Shift` actions for production inputs, cooking slots,
  fuel, harvesting, and planting.
- The menu lists Wood Cutting cleave as a passive skill feature.
- The Multiplayer roster updates while the menu is open when players report
  readiness, leave, or report a new compatibility state.
- `F7` copies the active Benheim diagnostic log to the player's Desktop with a
  timestamped filename on Mac and Windows.
- The game shows the exported filename so the player can find and attach the
  diagnostic log when reporting a problem.
