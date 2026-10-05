# Product Review

Only player-visible behavior that still needs Ben's hands belongs here.
Automated contracts, diagnostics, and exhaustive edge cases stay with the
feature that owns them.

Installed on Ben's Mac: **0.1.110**.

## Next play session

- **Spawn protection view:** At a base, press `F8` and use **Spawn protection
  overlay** in `Left Shift+B` > **Benheim Config**. Both should control the same
  plain green rings and grey `[no_spawn]` indicator beneath minimap danger.
  The view should start off in a fresh session. Walk across a protection
  boundary: the indicator should appear inside and disappear outside. Check
  that toggling the view off clears both visuals immediately and that enabling
  it around a busy base keeps gameplay responsive.
- **Put Away recovery:** After taking items from chests and placing a new chest,
  use `Left Shift+P` with a chest placement preview active, then use it again.
  Both attempts should finish without getting stuck on “already in progress.”
  Log out, rejoin, and confirm Put Away still works.
- **Blizzard Visibility:** During a native Mountain snowstorm, toggle Blizzard
  Visibility in `Left Shift+B` > **Benheim Config**. On should keep the native
  storm's snow, wind, audio, cold, and freezing while making distant terrain
  and Frost Cave entrances readable; off should restore the vanilla whiteout.
  Clear weather and other biomes should remain native.
- **Workbench and Stonecutter Hammer work zone:** At each level-1 station,
  cross the native, extended, and out-of-range boundaries while placing,
  repairing, and dismantling station-required pieces. The actions, dashed
  boundary, and station range shown in the Hammer UI should agree at each
  boundary. Crafting and station use, upgrade attachment, comfort, Workbench
  suppression and enemy spawning, and wards should remain native.

## Later solo play

- **Hoe precision:** When convenient, compare every native Hoe terrain action
  with `Left Shift` released and held. Check that the preview and affected area
  are 3x native radius only while held, and each click charges once.
- **Cultivator picker:** Check that clicking a new grid size closes the picker,
  and reopening it shows that size selected for the next preview and plant.
- **Pine drop details:** Check that a native Pine log half replaces all ordinary
  Wood with Core Wood, produces no Finewood, and keeps the native total count.
- **Auto-pickup details:** Check that an ordinary drop collects from roughly
  twice native reach. If convenient, try one floating in water or tar. Confirm
  that turning auto-pickup off, filling the inventory, or exceeding carry
  weight still blocks automatic collection.
- **Snipe:** Apply Snipe to a Huntsman Bow at a level-1 Forge for 1 Wood.
  Confirm that the Affinity tab looks native, the bow's title and description
  persist through ordinary inventory and storage, and applying Snipe twice is
  disabled. During use, confirm the zoom, clear-center vignette, slower draw,
  and close-range tradeoff feel right. Headshots should scale at roughly 20,
  40, and 60 meters while body shots and ordinary ammo behavior stay native.
- **Cleave tree lifecycle:** Use Cleave against a standing tree, its fallen
  log, and both log halves. Confirm that primary and nearby hits behave normally
  without lifecycle errors.
- **Sailing:** While steering, confirm that the upright speed gauge follows
  Valheim's wind UI. Hold Run at forward throttle and confirm the `SPRINT`
  state and stronger thrust; releasing Run, reversing, or leaving the helm
  must immediately restore native behavior.
- **Berry planting:** Confirm ordinary Blueberry and Cloudberry planting plus
  a centered `9x9` grid. Each bush must cost five matching berries, start
  empty, regrow, preserve state after reload, and return five berries when an
  authorized Hammer removal destroys a player-planted bush. Natural bushes
  must remain non-removable.
- **Lunge:** Apply Lunge to a max-quality Club at a level-1 Forge for 1 Wood.
  Confirm that the native-looking Affinity UI, title, and description are
  correct; the affinity persists through ordinary inventory, storage, drops,
  and reconnect; grounded swings remain native; and airborne primary swings
  produce the intended diagonal movement.

## When a compatible peer is available

- Confirm that earned-state audio is audible nearby but not at long distance.
- Confirm that berry placement, harvesting, removal authority, and regrowth
  remain shared and correct after reconnecting.
- Confirm that other players observe Lunge movement and that affinity state
  survives multiplayer storage and reconnects.
