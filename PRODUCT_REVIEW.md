# Product Review

This is the current acceptance queue for the Benheim client installed on Ben's
Mac. The owning `PRODUCT.md` defines required behavior. After Ben accepts an
item, remove it from this queue and update that product owner.

> **Reminder:** Discuss the operator-configuration problems before the next
> server deployment. The `0.1.66` integration first checked the wrong Doppler
> scope. It then selected the ignored `server.env` file, which the password
> guard rejects.

## Current candidate

- Ben's Mac has the private-test `0.1.74` client installed.
- The client installed from the private-test `0.1.74` package reached Valheim's
  main menu without Harmony or gameplay-disabled markers. Valheim then quit
  cleanly without loading a world.
- Benheim Server Support remains at `0.1.6`. The `0.1.74` client requires no
  change to that server component.

## Test on `0.1.74`

- **Workbench and Stonecutter range:** Place a Workbench-required piece around
  22 m and 38 m from an isolated level-1 Workbench. Confirm that placement fails
  beyond 40 m. Repeat with a Stonecutter-required piece. Station use, crafting,
  repair, and upgrades must keep their normal Valheim behavior.
- **Sailing:** While steering, confirm the fixed-position speed gauge follows
  actual motion and disappears when you leave the helm. Hold Run at forward
  throttle. Confirm that `SPRINT` appears and `3x` thrust applies. Release Run,
  reverse the throttle, and leave the helm. Each action must restore normal
  Valheim behavior.
- **Mass planting:** Place one plant normally. Then preview and place the
  centered 9x9 grid. Each successful ordinary or grid placement costs 25% of
  the native stamina cost after Valheim applies the Farming skill adjustment.
  A failed, skipped, or rejected placement costs no stamina.
- **Finewood:** Break native Birch and Oak logs. Every ordinary Wood item from
  those logs must become Finewood. The conversion must preserve the exact item
  count and Valheim's native drop behavior. In multiplayer, break a Birch or
  Oak log that another compatible player owns. Confirm the same conversion,
  item count, and native drop behavior. Drops from other logs and stumps must
  remain unchanged. Non-Wood drops from Birch and Oak logs must also remain
  unchanged.
- **Tar:** Manually collect native Tar while it is submerged. Items other than
  Tar must remain stuck. No item may be collected automatically.
- **Perfect Impact:** While airborne and descending, land one melee hit on a
  character. A hit that meets the owning product's qualification rule shows one
  `PERFECT IMPACT`. Other hits keep normal Valheim behavior. Combat Shake
  controls only the optional shake.
