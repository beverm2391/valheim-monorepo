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

- Packaged version: private-test `0.1.77` for Mac and Windows. This version is
  not installed and has no packaged-build startup proof.
- Installed version: private-test `0.1.76` on Ben's Mac, installed from the
  exact macOS package.
- Startup proof: The managed Benheim launcher started the exact installed macOS
  package. It reached the real main menu in Valheim `0.221.12`. The fresh log
  contained the expected version, session-start, and chainloader-complete
  markers. The log contained no Harmony cleanup marker, core-disablement
  marker, gameplay-disabled marker, or world-load marker. No world was entered.
  The task quit only the Valheim process that it launched, and no Valheim
  process remained.
- Benheim Server Support remains at `0.1.6`. Clients `0.1.75` through `0.1.77`
  require no change to that server component.

## Test on installed `0.1.76`

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
- **Perfect Impact:** While airborne and descending, land one melee hit on a
  character. A hit that meets the owning product's qualification rule shows one
  `PERFECT IMPACT`. Other hits keep normal Valheim behavior. Combat Shake
  controls only the optional shake.
- **Earned-state audio:** In multiplayer, trigger an earned combat state near
  one compatible player and far from another. The nearby player may hear the
  native charm cue. The distant player must not hear it.

## After `0.1.77` installation

Keep these checks out of the installed-client queue until the exact `0.1.77`
package is installed and passes startup proof.

- **Berry planting:** For each native Raspberry, Blueberry, and Cloudberry bush:

  - confirm ordinary placement and centered 9x9 placement;
  - confirm that each placement costs exactly five matching berries;
  - harvest the bush and confirm its native 300-minute respawn;
  - reload the save and confirm persistence; and
  - in multiplayer, confirm shared placement and harvesting, creator ownership,
    and reconnect behavior.
- **Sign glow:** Judge the warm portal-amber letter glow on an existing wooden
  sign. The wooden board must not glow.
- **Portal labels:** On wooden and stone portals, confirm that each label appears
  above the portal and exactly matches its current tag. Confirm that the label
  is visible at 30 meters but not beyond, hides when the player's line of sight
  is blocked, and updates after the portal tag is renamed.
- **Comfort summary:** Run `bh debug comfort`. Confirm that the console shows a
  readable summary with the calculated comfort and **Counted**, **Ignored**, and
  **Just outside range** sections.
