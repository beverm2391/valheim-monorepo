# Product Review

This is the current release ledger and acceptance queue for Benheim client
releases. Live acceptance applies to the client installed on Ben's Mac. The
owning `PRODUCT.md` defines required behavior. `PROMPT.md` owns who updates this
ledger and who can accept its items.

> **Reminder:** Discuss the operator-configuration problems before the next
> server deployment. The `0.1.66` integration first checked the wrong Doppler
> scope. It then selected the ignored `server.env` file, which the password
> guard rejects.

## Release state

- Packaged version: private-test `0.1.76` for Mac and Windows. This version is
  not installed and has no packaged-build startup proof.
- Installed version: private-test `0.1.75` on Ben's Mac. It is currently
  running.
- Benheim Server Support remains at `0.1.6`. Clients `0.1.75` and `0.1.76`
  require no change to that server component.

## Test on installed `0.1.75`

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
- **Finewood:** Break native Birch and Oak logs. Every ordinary Wood item from
  those logs must become Finewood. The conversion must preserve the exact item
  count and Valheim's native drop behavior. In multiplayer, break a Birch or
  Oak log that another compatible player owns. Confirm the same conversion,
  item count, and native drop behavior. Drops from other logs and stumps must
  remain unchanged. Non-Wood drops from Birch and Oak logs must also remain
  unchanged.
- **Tar:** Manually collect native Tar while it is submerged. Items other than
  Tar must remain stuck. No item may be collected automatically.
- **Comfort:** Put comfort furniture between 10 m and 20 m away in a nearby
  room, floor, or building. Confirm that it can contribute comfort while native
  furniture values, grouping, shelter, fire, and Rested behavior stay unchanged.
  Run `bh debug comfort` once. Confirm that the log records the radius, shelter,
  comfort state, which candidates Valheim's native comfort query counted or
  skipped, why it made each decision, and the nearest pieces excluded by radius.
  The command must not change the player or world.
- **Perfect Impact:** While airborne and descending, land one melee hit on a
  character. A hit that meets the owning product's qualification rule shows one
  `PERFECT IMPACT`. Other hits keep normal Valheim behavior. Combat Shake
  controls only the optional shake.

## After `0.1.76` installation

Keep these checks out of the installed-client queue until the exact `0.1.76`
package is installed and passes startup proof.

- **Earned-state audio:** In multiplayer, trigger an earned combat state near
  one compatible player and far from another. The nearby player may hear the
  native charm cue. The distant player must not hear it.
