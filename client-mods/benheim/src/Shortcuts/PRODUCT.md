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

- The current candidate adds an Affinities section. The
  section says that binding Lunge to one specific max-quality Club at a Forge
  costs 1 Wood. The section says that Lunge stays with that item. It says that
  an airborne primary swing adds a 10 m/s forward impulse and raises vertical
  velocity to at least +3 m/s. Grounded Club swings remain native. It also says
  that replacing the Affinity does not refund the materials for the prior
  Affinity. The updated presentation still needs gameplay proof.
- The `0.1.77` candidate adds organized feature entries for Ship Sprint,
  manual collection of submerged native Tar, Perfect Impact, and reduced
  planting stamina. The planting-stamina entry says that each successful
  ordinary or grid plant placement costs 25% of the native planting stamina
  cost that Valheim has already resolved. Skipped, failed, and rejected
  placements cost no stamina. It preserves the menu's named combat states and
  useful tuning detail. UNTOUCHABLE now says that qualifying kills as well as
  perfect defenses add streak points. Combat Shake also names Perfect Impact.
  The updated presentation still needs gameplay proof.
- The Ship Sprint entry explains that the helm readout shows planar speed and
  marks `SPRINT` while the local player has an active Ship Sprint request.
- The `0.1.77` candidate adds a Building section. It says Workbench and
  Stonecutter build-piece placement coverage is `2x` Valheim's native range,
  from 20 meters to 40 meters for level-1 stations. It also names the station
  behaviors that remain native.
- The candidate renames the Skills section to **Gathering & Skills**. It adds a
  Finewood entry. The entry says that the compatible client that owns a native
  Birch or Oak log converts each final ordinary Wood drop to Finewood. This
  conversion also works when another compatible client attacks the log. Each
  log keeps its native item count unchanged. Valheim still spawns every drop
  through its native path. Native Finewood drops and all non-Wood drops remain
  unchanged. For the Finewood conversion, other logs, standing-tree drops,
  stumps, native damage-type conversions, and unrelated destruction stay
  native.
- The Farming section lists `1`, `3`, `5`, `7`, and `9` as selectable centered
  grid sizes while the Cultivator picker is open. It says each Cultivator-picker
  session starts with the 9x9 grid selected. It also lists native Raspberry,
  Blueberry, and Cloudberry bushes. It says planting each bush costs five
  matching berries and requires only ordinary ground. Every grid uses each
  native bush's collision boundary for spacing.
- The World & Travel section lists sign glow and portal labels. It says that
  each portal label exactly matches its portal's non-empty tag. It also states
  the 30-meter range and line-of-sight rule.
- The Combat section says compatible nearby players may hear the native charm
  cue for earned states. It says distant players must not hear the cue.
- The `/` shortcut never closes or toggles the console. F5 and Escape keep
  their native behavior. The shortcut does nothing while the player is typing
  or while a menu, password field, or other text input is active. This
  suppression still needs gameplay proof. Each relevant `/` keypress produces
  one typed `Shortcuts.native_console_shortcut` event. The event records an
  `opened` or `rejected` result and the exact stable reason.
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
