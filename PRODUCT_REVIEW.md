# Product Review

This is the current release ledger and acceptance queue for Benheim client
releases. Live acceptance applies to the client installed on Ben's Mac. The
owning `PRODUCT.md` defines required behavior. `PROMPT.md` owns who updates this
ledger and who can accept its items.

## Release state

- Packaged version: private-test `0.1.79` for Mac and Windows. The Windows
  package remains packaged-only.
- Installed version: private-test `0.1.79` on Ben's Mac, installed from the
  exact private-test macOS package.
- Startup proof: The managed Benheim launcher started the exact installed
  `0.1.79` package. It reached the real main menu in Valheim `0.221.12`. The
  fresh log contained the expected version, session-start,
  chainloader-complete, and clean session-end markers. The log contained no
  Harmony cleanup marker, core-disablement marker, gameplay-disabled marker,
  or world-load marker. No world was entered. The task quit only the Valheim
  process that it launched, and no Valheim process remained.
- Benheim Server Support remains at `0.1.6`. Clients `0.1.75` through `0.1.79`
  require no change to that server component.

## Test on installed `0.1.79`

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
- **Mass planting:** Place one plant normally. Then preview and place the
  centered 9x9 grid. Each successful ordinary or grid placement costs 25% of
  the native stamina cost after Valheim applies the Farming skill adjustment.
  A failed, skipped, or rejected placement costs no stamina.
- **Tar:** Manually collect native Tar while it is submerged. Items other than
  Tar must remain stuck. No item may be collected automatically.
- **Perfect Impact:** Ben confirmed that the first qualifying live hit showed
  `PERFECT IMPACT`. Still confirm that nonqualifying hits keep normal Valheim
  behavior. Confirm that Combat Shake controls only the optional shake.
- **Earned-state audio:** In multiplayer, trigger an earned combat state near
  one compatible player and far from another. The nearby player may hear the
  native charm cue. The distant player must not hear it.

- **Comfort summary:** Run `bh debug comfort`. Confirm that the console shows a
  readable summary with the calculated comfort and **Counted**, **Ignored**, and
  **Just outside range** sections.
- **Berry planting:** For each native Raspberry, Blueberry, and Cloudberry bush:

  - confirm ordinary placement and centered 9x9 placement;
  - confirm that each placement costs exactly five matching berries;
  - harvest the bush and confirm its native 300-minute respawn;
  - reload the save and confirm persistence; and
  - in multiplayer, confirm shared placement and harvesting, creator ownership,
    and reconnect behavior.
- **Cultivator grid selection:** Open the Cultivator picker and press `1`, `3`,
  `5`, `7`, and `9`. Confirm each key immediately selects and previews the
  matching centered grid. Confirm that each Cultivator-picker session starts
  with the 9x9 grid selected. Confirm that even number keys change neither the
  grid nor the hotbar. Confirm that number keys regain native behavior when the
  Cultivator picker closes.
- **Portal labels:** On wooden and stone portals, confirm that each label appears
  above the portal and exactly matches its current tag. Confirm that the label
  is visible at 30 meters but not beyond, hides when the player's line of sight
  is blocked, and updates after the portal tag is renamed.
- **Leech spawning:** With compatible clients owning active Swamp zones, confirm
  ordinary base-world Leech opportunities use the 5x rate. Confirm each
  successful adjusted spawn records one event with source `base_world`, prefab
  `Leech`, and multiplier `5`. Transfer zone ownership and confirm the same
  behavior continues.
- **Club + Lunge Affinity:** At a base-game Forge, confirm that an Affinity tab
  appears beside Craft and Upgrade. Switch back to Craft and Upgrade, then
  confirm that each native tab returns unchanged. Spend 1 Wood to apply Lunge
  to one specific max-quality Club. Confirm that an ineligible weapon cannot
  receive Lunge. Apply Lunge again as a replacement and confirm that it destroys
  the prior Affinity without refunding the materials for the prior Affinity.
  Move, equip, store, and drop the Club, then reconnect. Confirm that the same
  Club retains Lunge after every action. An airborne primary swing must add one
  10 m/s forward impulse and raise its vertical velocity to at least +3 m/s.
  Grounded Club swings must remain native. Use debug inspect, apply,
  clear, and session-force commands only to isolate a failure. If a compatible
  peer is available, confirm that the peer sees the Lunge movement.
