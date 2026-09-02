# Product Review

This is the current release ledger and acceptance queue for Benheim client
releases. Live acceptance applies to the client installed on Ben's Mac. The
owning `PRODUCT.md` defines required behavior. `PROMPT.md` owns who updates this
ledger and who can accept its items.

## Release state

- Packaged version: private-test `0.1.81` for Mac and Windows. The Windows
  package remains uninstalled.
- Installed version: private-test `0.1.81` on Ben's Mac, installed from the
  exact private-test macOS package.
- Startup proof: The latest proof applies only to installed `0.1.80`. The
  managed Benheim launcher started that exact package and reached the real main
  menu in Valheim `0.221.12`. The
  fresh log contained the expected version, session-start,
  chainloader-complete, and clean session-end markers. The log contained no
  Harmony cleanup marker, core-disablement marker, gameplay-disabled marker,
  or world-load marker. No world was entered. The task quit only the Valheim
  process that it launched, and no Valheim process remained. Installed `0.1.81`
  has no startup proof.
- Benheim Server Support remains at `0.1.6`. Clients `0.1.75` through `0.1.81`
  require no change to that server component.

## Test on installed `0.1.81`

- **Earned-state audio:** In multiplayer, trigger an earned combat state near
  one compatible player and far from another. The nearby player may hear the
  native charm cue. The distant player must not hear it.

- **Workbench and Stonecutter range:** Place a Workbench-required piece around
  22 m and 38 m from an isolated level-1 Workbench. Confirm that placement fails
  beyond 40 m. Repeat with a Stonecutter-required piece. Station use, crafting,
  repair, and upgrades must keep their normal Valheim behavior.
- **Sailing:** While steering, confirm the upright speed gauge sits directly
  below Valheim's native wind UI on the right and follows that UI. It must show
  planar speed and disappear when you leave the helm. Hold Run at forward
  throttle. Confirm that `SPRINT` appears and `3x` thrust applies. Release Run,
  reverse the throttle, and leave the helm. Each action must restore normal
  Valheim behavior.
- **Developer command discovery:** In Valheim's built-in console, confirm that
  the console completes the first argument for each command: `bhcatalog`,
  `bhrun`, and `bhwatch`. Run the effects, text, and UI catalog commands.
  Confirm that each snapshot returns a result within its defined limit and
  leaves no temporary state in the running game.
- **Leech spawning:** With compatible clients owning active Swamp zones, confirm
  ordinary base-world Leech opportunities use the 5x rate. Confirm each
  successful adjusted spawn records one event with source `base_world`, prefab
  `Leech`, and multiplier `5`. Transfer zone ownership and confirm the same
  behavior continues.

- **Tar-pit pickup:** Installed `0.1.80` proved manual pickup of submerged
  native Tar but left other items stuck and did not auto-pick up. Drop native
  Tar, Stone, and one other ordinary item into a native tar pit. Confirm that
  each item supports normal manual pickup and normal auto-pickup. Confirm that
  native range, inventory-space, carry-weight, and ownership failures still
  block collection normally.

- **Farming and Cultivator grids:** Installed `0.1.80` exposed an input-boundary
  failure. Open the Cultivator picker and confirm that each picker session
  starts with 9x9 selected. Hold `Left Shift` and press each of `1`, `3`, `5`,
  `7`, and `9`. After each selection, confirm that the existing `Left Shift`
  mass-plant preview and placement both use the matching centered grid. Plain
  number keys and every other number-key combination must keep native behavior.
  Place one plant normally, then place a centered 9x9 grid containing valid and
  invalid cells. Each successful normal or grid-cell placement must cost 25%
  of the native stamina cost after Valheim applies the Farming skill adjustment.
  A failed, skipped, or rejected placement must cost no stamina.
- **Comfort summary:** Installed `0.1.80` accepted the Comfort calculation and
  typed evidence but flooded Valheim's non-scrollable console. Run
  `bhrun comfort`. Confirm that the console shows a short readable summary with
  calculated comfort and counts for **Counted**, **Ignored**, and **Just outside
  range**.
  Confirm that complete per-piece evidence remains in typed diagnostics.
- **Berry planting:** Installed `0.1.80` proved ordinary Raspberry placement
  but exposed the initial-growth failure: the new bush did not start empty.
  Retest Raspberry, Blueberry, and Cloudberry after the correction:

  - confirm ordinary Blueberry and Cloudberry placement and centered 9x9
    placement for all three bushes;
  - confirm that each placement costs exactly five matching berries;
  - confirm that each newly planted bush starts empty;
  - confirm that Benheim assigns each planted or naturally spawned bush a wait
    of 4,000 to 5,000 seconds before each yield, including the first yield of a
    planted bush;
  - confirm unrelated `Pickable` objects keep native timing;
  - use the Hammer to remove one player-planted bush of each type and confirm
    each returns exactly five matching berries when native access and ward
    rules allow removal;
  - confirm naturally spawned bushes cannot be removed with the Hammer and the
    Cultivator removes no planted or naturally spawned berry bush;
  - reload the save and confirm persistence; and
  - in multiplayer, confirm shared placement and harvesting, creator ownership,
    and reconnect behavior.
- **Portal labels:** Installed `0.1.80` displayed and updated portal tags, but
  its world lettering was not readable enough against scenery. On wooden and
  stone portals, confirm that each corrected label remains fixed just above its
  portal and uses the same high-contrast overlay treatment as Perfect Parry
  feedback. Confirm that it exactly matches the current non-empty tag, is
  visible at 30 meters but not beyond, hides when line of sight is blocked, and
  updates after a rename. Give a portal an empty tag and confirm no label appears.
- **Club + Lunge Affinity:** Confirm that the Affinity tab shows Forge level `1`
  in the native station-requirement slot and keeps Wood in the following
  material slot. Spend 1 Wood to apply Lunge to an eligible Club that does not
  already have Lunge. In the ordinary inventory, confirm that the weapon title is
  `Club · Lunge` and its hover description preserves the native Club text while
  adding Lunge's behavior and persistent bias. Switch from Affinity back to
  Craft and Upgrade and confirm that each native tab returns unchanged. Move,
  equip, store, and drop the Club, then reconnect. Confirm that the same Club
  retains Lunge after every action. Grounded Club swings must remain native.
  If a compatible peer is available, confirm that the peer sees the Lunge
  movement.
- **Developer probes:** Run `bhwatch` and confirm that `spawns` is enabled by
  default and `colliders` is disabled by default. Confirm that `spawns` records
  the registered Leech rule, bounded population changes, cap transitions, and a
  low-frequency
  heartbeat without changing spawn behavior. Enable `colliders` before each
  independent cleanup check. Then test `off`, `default`, world exit, and logout.
  Confirm that every path removes all overlay objects. Confirm that the default
  state is disabled.
