# Product Review

Only player-visible behavior that still needs Ben's hands belongs here.
Automated contracts, diagnostics, and exhaustive edge cases stay with the
feature that owns them.

Installed on Ben's Mac: **0.1.101**.

## Next play session

- **Cultivator grid selection:** Open the Cultivator picker and confirm that
  the `1x1`, `3x3`, `5x5`, `7x7`, and `9x9` row appears with `5x5`
  highlighted. Select another size, reopen the picker, and confirm that the
  choice remains highlighted and controls the next `Left Shift` preview and
  placement. The Hammer must show no row, and number keys must remain native.
- **Floating drops:** Drop Tar, Stone, and another ordinary item into water.
  Confirm that each rises, settles without launching upward, and remains
  pickable. Repeat in a tar pit when convenient.
- **Pine specialty wood:** Destroy one native Pine log half. Confirm that all
  ordinary Wood becomes Core Wood, Pine produces no Finewood, and the total
  native drop count remains unchanged.

## Later solo play

- **Native adrenaline unlock:** Before native Adrenaline is unlocked, perfect
  defenses must produce no Benheim reward text, Adrenaline, or UNTOUCHABLE
  state. After the native unlock, the same actions must activate Benheim's
  feedback and rewards normally.
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
