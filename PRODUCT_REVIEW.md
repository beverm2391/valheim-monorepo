# Product Review

Outstanding hands-on checks for the Benheim build currently installed on Ben's
Mac.

Installed on Ben's Mac: **0.1.93**.

Grid selection, planting, and berry cycles record diagnostics without enabling
a probe.

## Valheim 1.0 migration state

This is the single coordination view for migration state. Exact commits,
artifacts, logs, and runtime records stay in Git, the canonical verification
commands, Valheim Dev `lab_status`, and the dedicated-server journal.

| Component | Static/build | Installed | Runtime | Remaining human proof |
| --- | --- | --- | --- | --- |
| Valheim Dev bridge | Passes its canonical verification on 1.0.7. | Yes, `0.3.0`. | Connected in the disposable world on network version `39`. | Only visible recipe outcomes that require Ben's judgment. |
| Benheim client | The 1.0 port builds; focused checks pass for three follow-up fixes. | Yes; the next exact candidate is not installed yet. | Failed. `0.1.95` starts cleanly, but deep QA found the Shortcut collision crash, split-dialog subscriber leak, and rapid Snipe FOV collapse. | Install and retest the next exact candidate, then complete the checks below. |
| Benheim Eternal Fire | Not yet assessed against the exact 1.0 Linux server assembly. | No on QA. | Not started. | Prove supported fires refill and still do so after restart. |
| Benheim Server Support | Not yet assessed against the exact 1.0 Linux server assembly. | No on QA. | Not started. | Prove Put Away authority, peer lifecycle, item provenance, and confirmed-kill delivery. |
| Benheim Test Commands | Not yet assessed against the exact 1.0 Linux server assembly. | No on QA. | Not started. | Prove administrator requests, Boar ownership migration, and the read-only henge overlay. |

The legacy `benheim-inventory` path is not a separate live mod. Its active
client behavior belongs to Benheim, its dedicated-server behavior belongs to
Benheim Server Support, and the shared protocol remains implementation support.

## Remaining checks

- **Snipe application:** At a level-1 Forge, spend 1 Wood to apply Snipe to a
  Huntsman Bow at any native quality. Confirm its inventory title and
  description, the disabled same-affinity action, and persistence after storage
  and reconnect.
  The station-level requirement icon beneath the level must reuse Valheim's
  native gold star, matching the other crafting-station requirements.
  The Affinity tab must sit beneath the Forge panel's bronze divider at the
  same depth and alignment as the native Craft and Upgrade tabs.
  Other bows must remain ineligible. Native upgrades may erase the affinity;
  confirm that it can be applied again afterward.
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
- **Leech spawning:** The interval between Leech spawn opportunities is
  confirmed at one-fifth of normal. A successful adjusted spawn and a
  zone-owner transfer remain unproven.
  Stay in an active Swamp zone until one logged
  success records source `base_world`, prefab `Leech`, and multiplier `5`. Then
  transfer zone ownership to another compatible client and confirm that the
  same behavior continues.

- **Tar-pit pickup:** Manual pickup of submerged Tar worked, but other items
  remained stuck and auto-pickup failed. Valheim disables auto-pickup for items
  dropped from inventory. Use naturally dropped loot for auto-pickup checks.
  Retest the correction with native Tar, Stone, and one other ordinary item in
  a native tar pit. Confirm that each item supports normal manual pickup and
  normal auto-pickup. Confirm that
  native range, inventory-space, carry-weight, and ownership failures still
  block collection normally.

- **Farming stamina:** Place one plant normally, then use `Left Shift` mass
  planting on an area that contains valid and
  invalid cells. Each successful normal or grid-cell placement must cost 25%
  of the native stamina cost after Valheim applies the Farming skill adjustment.
  A failed, skipped, or rejected placement must cost no stamina.
- **Cultivator grid selection:** In one plugin session, open the Cultivator
  picker and confirm that 5x5 is highlighted. Select another size, close the
  picker, and reopen it without restarting the game. Confirm that the selected
  size remains highlighted and controls the next `Left Shift` preview and
  placement. The Hammer picker must have no grid-size row, and number keys must
  keep their native behavior. Fully quit and relaunch the game to start a fresh
  plugin session. Confirm that the Cultivator picker starts at 5x5.
- **Comfort summary:** The Comfort calculation is accepted. Test the shorter
  output for Valheim's non-scrollable console. Run
  `bhrun comfort`. Confirm that the console shows a short readable summary with
  calculated comfort and counts for **Counted**, **Ignored**, and **Just outside
  range**.
  Confirm that complete per-piece evidence remains in typed diagnostics.
- **Berry planting:** Raspberry placement worked. Ben accepted Hammer removal
  with a five-berry refund. The new bush previously started with berries. We
  still need to confirm that newly planted bushes start empty. Test the
  remaining berry behavior:

  - confirm ordinary Blueberry and Cloudberry placement and centered 9x9
    placement for all three bushes;
  - for Raspberry, Blueberry, and Cloudberry, measure adjacent grid positions
    along each grid axis. Confirm exactly 1.75 meters between adjacent preview
    positions and between the corresponding placed bushes. Confirm that
    default-on logs report 1.75 meters for both preview and placement;
  - confirm that each placement costs exactly five matching berries;
  - confirm that each newly planted bush starts empty;
  - confirm that Benheim assigns each planted or naturally spawned bush a wait
    of 4,000 to 5,000 seconds before each yield, including the first yield of a
    planted bush;
  - use default-on logs to follow one bush's cycle start, chosen duration,
    actual transition to harvestable, and harvest; distinguish a state seen on
    loading from a transition observed while the bush was loaded;
  - confirm unrelated `Pickable` objects keep native timing;
  - use the Hammer to remove one player-planted Blueberry bush and one
    player-planted Cloudberry bush; confirm that each returns exactly five
    matching berries when native access and ward rules allow removal;
  - confirm naturally spawned bushes cannot be removed with the Hammer and the
    Cultivator removes no planted or naturally spawned berry bush;
  - reload the save and confirm persistence; and
  - in multiplayer, confirm shared placement and harvesting, creator ownership,
    and reconnect behavior.
- **Pine Finewood:** Destroy one native Pine log half. Confirm that it produces
  15 native item drops. Confirm that none is ordinary Wood and that the drops
  still include Core Wood. If a compatible peer is available, let a
  non-owner deliver the final hit once and confirm the owner still converts the
  drops.
- **Club + Lunge Affinity:** Confirm that the Affinity tab shows Forge level `1`
  in the native station-requirement slot and keeps Wood in the following
  material slot. Choose a max-quality Club without Lunge. Spend
  1 Wood to apply Lunge. In the ordinary inventory, confirm that the weapon
  title is `Club · Lunge` and its hover description preserves the native Club text while
  adding Lunge's behavior and persistent bias. Switch from Affinity back to
  Craft and Upgrade and confirm that each native tab returns unchanged. Move,
  equip, store, and drop the Club, then reconnect. Confirm that the same Club
  retains Lunge after every action. Grounded Club swings must remain native.
  In ordinary multiplayer play, if a compatible peer is available, confirm
  that the peer sees the Lunge movement. Separately, in a disposable local
  world, equip a below-max-quality native Club and run
  `bh debug affinity apply lunge`. Confirm that an airborne primary swing
  Lunges despite the normal Forge quality requirement.
- **Developer probes:** Run `bhwatch` and confirm that `spawns` is enabled by
  default and `colliders` is disabled by default. Confirm that `spawns` records
  the registered Leech rule, bounded population changes, cap transitions, and a
  low-frequency
  heartbeat without changing spawn behavior. Enable `colliders` before each
  independent cleanup check. Then test `off`, `default`, world exit, and logout.
  Confirm that every path removes all overlay objects. Confirm that the default
  state is disabled.
