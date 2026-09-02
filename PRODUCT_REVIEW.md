# Product Review

This is the current release ledger and acceptance queue for Benheim client
releases. Live acceptance applies to the client installed on Ben's Mac. The
owning `PRODUCT.md` defines required behavior. `PROMPT.md` owns who updates this
ledger and who can accept its items.

## Release state

- Packaged version: private-test `0.1.83` for Mac and Windows from source commit
  `cc75d53f25e5d8f42aed20ca1e255fb11e4152f1`. Neither package is installed.
  The macOS package SHA-256 is
  `3a25295e5e6a646e415396807ed7b059df07bfbb93f62e6a76bfded8c226bdae`.
  The Windows package SHA-256 is
  `d0fc27b21786a1af3f274b83250fc7285ee47620f7bdc33de7f9cb85c6c663f5`.
  Both archives contain DLL SHA-256
  `eada436029928e4903569c93d7ae5867aac6b93b3690a3a29a96e6563a2004d3`.
  Their private-diagnostics build IDs match that DLL. Both archives and their
  private-diagnostics configurations have owner-only permissions. Canonical
  verification passed; startup and gameplay proof for `0.1.83` remain pending.
- Installed version: private-test `0.1.82` on Ben's Mac, unchanged by `0.1.83`
  packaging. It came from source commit
  `4bf61c24e9be8ef1bf764861dfbc8d22bdac6375` and macOS package SHA-256
  `b3659e7c51da0693ff2f03408bec79cd51b65a6d80c95ee3e8232918e1117ab8`.
  The installed DLL SHA-256 is
  `58dbd71f2a413271258381ee6ce7bbc511d3131a6ea3cc3bb7ecabcba566baf0`.
  The installed private-diagnostics build ID matches the installed DLL. The
  installed private-diagnostics configuration remains readable only by the
  owner.
- Installed `0.1.82` startup proof: The managed Benheim launcher started Valheim
  `0.221.12` with the exact installed `0.1.82` package and reached the real main menu. The main
  menu was visually confirmed. The fresh log contained the expected version,
  session-start, chainloader-complete, menu-music, and clean session-end
  markers. The log contained no Harmony cleanup marker, core-disablement
  marker, gameplay-disabled marker, or world-load marker. No world was entered.
  The task quit only the Valheim process that it launched, and no Valheim
  process remained.
- Benheim Server Support remains at `0.1.6`. Clients `0.1.75` through `0.1.83`
  require no change to that server component.

## Test on packaged `0.1.83` after installation

These checks wait for installation and startup proof. They do not apply to the
currently installed `0.1.82` client.

- **Snipe application:** At a level-1 Forge, spend 1 Wood to apply Snipe to a
  max-quality Huntsman Bow. Confirm its inventory title and description, the
  disabled same-affinity action, and persistence after storage and reconnect.
  Lower-quality Huntsman Bows and other bows must remain ineligible.
- **Snipe handling:** Draw, fire, and cancel with Bow Focus and Benheim FX off.
  Confirm useful 3x zoom, soft edges that darken with draw progress, a clear
  center, and an immediate return to normal view. Compare with an ordinary
  Huntsman Bow: Snipe should take 25% longer to reach full draw while keeping
  native partial shots and stamina use. Check the close-range tradeoff by feel.
- **Snipe headshots:** Land headshots near 20 m, 40 m, and 60 m. Confirm total
  multipliers of 1.25x, 1.75x, and 2.25x, including a partial draw and an arrow
  that hits after switching weapons. Body shots, native WeakSpots, and ammo
  effects must retain their normal behavior.
- **Cleave tree lifecycle:** Chop a standing tree and the new log and log
  halves. Confirm normal primary and nearby Cleave hits without tree-lifecycle
  errors.
- **Wisp discovery:** Run `bhrun wispecho` in a loaded world. Confirm a bounded
  discovery summary and matching detailed diagnostics. This build adds no
  mead, Wisp Echo rendering, or cyan highlights.

## Test on installed `0.1.82`

- **Earned-state audio:** In multiplayer, trigger an earned combat state near
  one compatible player and far from another. The nearby player may hear the
  native charm cue. The distant player must not hear it.

- **Workbench and Stonecutter range:** Place a Workbench-required piece around
  22 m and 38 m from an isolated level-1 Workbench. Confirm that placement fails
  beyond 40 m. Repeat with a Stonecutter-required piece. Station use, crafting,
  repair, and upgrades must keep their normal Valheim behavior.
- **Feast range:** Stand between 3.5 m and 8 m from a native Feast, then use and
  eat from it. Confirm that the Feast remains unavailable beyond 8 m. Confirm
  that other interactable objects remain available up to 8 m and that open
  containers remain available up to 10 m.
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
- **Leech spawning:** The installed `0.1.81` build emitted a diagnostic for the
  configured interval of ordinary base-world Leech spawns: 200 native seconds,
  40 effective seconds, factor `5`. This proves the approved 5x opportunity
  configuration, not a successful spawn. The spawn watcher observed loaded
  counts only from 0 through 2. It never saturated the native cap of 10, so the
  cap did not limit this session. The inspected log contained no
  `leech_spawn_succeeded` event. A fresh successful adjusted spawn and a
  zone-owner transfer remain unproven. Stay in an active Swamp zone until one
  success records source `base_world`, prefab `Leech`, and multiplier `5`. Then
  transfer zone ownership to another compatible client and confirm that the
  same behavior continues.

- **Tar-pit pickup:** Installed `0.1.80` proved manual pickup of submerged
  native Tar but left other items stuck and did not auto-pick up. Drop native
  Tar, Stone, and one other ordinary item into a native tar pit. Confirm that
  each item supports normal manual pickup and normal auto-pickup. Confirm that
  native range, inventory-space, carry-weight, and ownership failures still
  block collection normally.

- **Farming and Cultivator grids:** Installed `0.1.81` did not select a grid
  when the player held `Left Shift` and pressed an odd-number key. In installed
  `0.1.82`, open the Cultivator picker and confirm that each picker session
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
  In live `0.1.81` testing, Ben removed one player-planted Raspberry bush with
  the Hammer and received exactly five Raspberries. Ben accepted this tested
  case.
  Retest Raspberry, Blueberry, and Cloudberry after the correction:

  - confirm ordinary Blueberry and Cloudberry placement and centered 9x9
    placement for all three bushes;
  - confirm that each placement costs exactly five matching berries;
  - confirm that each newly planted bush starts empty;
  - confirm that Benheim assigns each planted or naturally spawned bush a wait
    of 4,000 to 5,000 seconds before each yield, including the first yield of a
    planted bush;
  - confirm unrelated `Pickable` objects keep native timing;
  - use the Hammer to remove one player-planted Blueberry bush and one
    player-planted Cloudberry bush; confirm that each returns exactly five
    matching berries when native access and ward rules allow removal;
  - confirm naturally spawned bushes cannot be removed with the Hammer and the
    Cultivator removes no planted or naturally spawned berry bush;
  - reload the save and confirm persistence; and
  - in multiplayer, confirm shared placement and harvesting, creator ownership,
    and reconnect behavior.
- **Portal labels:** On tagged wooden and stone portals, confirm that a
  two-sided Valheim wooden sign board stays fixed 20 to 30 cm above the portal
  instead of facing the player. Confirm that its glowing letters exactly match
  the current tag, scene geometry occludes it normally, and renaming updates
  both sides. Give a portal an empty tag and confirm that no board appears.
- **Pine Finewood:** Destroy one native Pine log half. Confirm that it produces
  15 native item drops. Confirm that none is ordinary Wood and that the drops
  still include Core Wood. If a compatible peer is available, let a
  non-owner deliver the final hit once and confirm the owner still converts the
  drops.
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
