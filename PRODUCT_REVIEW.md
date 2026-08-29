# Product Review

This is the current acceptance queue for the Benheim client installed on Ben's
Mac. The owning `PRODUCT.md` defines required behavior. After Ben accepts an
item, remove it from this queue and update that product owner.

> **Reminder:** Discuss the operator-configuration problems before the next
> server deployment. The `0.1.66` integration first checked the wrong Doppler
> scope. It then selected the ignored `server.env` file, which the password
> guard rejects.

## Current candidate

- Ben's Mac has the private-test `0.1.73` client installed.
- Clients `0.1.70` through `0.1.72` contain the broken Farming startup matcher
  and must not be used.
- The `0.1.73` hotfix changes only that startup matcher. In all three launches,
  the installed client reached Valheim's main menu without Harmony or
  gameplay-disabled markers. The client quit before loading a world each time.
- Server Support remains `0.1.6`. This client hotfix needs no server update.

## Test on `0.1.73`

- **Workbench and Stonecutter range:** Place a Workbench-required piece around
  22 m and 38 m from an isolated level-1 Workbench. Confirm that placement fails
  beyond 40 m. Repeat with a Stonecutter-required piece. Station use, crafting,
  repair, and upgrades must keep their normal Valheim behavior.
- **Sailing:** While steering, confirm the fixed-position speed gauge follows
  actual motion and disappears when you leave the helm. Hold Run at forward
  throttle. Confirm that `SPRINT` appears and `3x` thrust applies. Release Run,
  reverse the throttle, and leave the helm. Each action must restore normal
  Valheim behavior.
- **Mass planting:** Preview and place the centered 9x9 grid. Each successful
  plant still costs half the normal Valheim stamina cost in this candidate. A
  failed planting attempt costs no stamina.
- **Tar:** Manually collect native Tar while it is submerged. Items other than
  Tar must remain stuck. No item may be collected automatically.
- **Perfect Impact:** While airborne and descending, land one melee hit on a
  character. A hit that meets the owning product's qualification rule shows one
  `PERFECT IMPACT`. Other hits keep normal Valheim behavior. Combat Shake
  controls only the optional shake.
## Not in `0.1.73`

- The approved 25% Farming stamina cost moves to the next feature candidate.
- Skill-scaled Finewood needs a small multiplayer message that includes the
  player's skill level. Valheim does not send a remote player's Wood Cutting
  level to the player who owns the log.
