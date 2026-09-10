# Benheim Eternal Fire

Benheim Eternal Fire keeps supported native Valheim fire and light pieces fueled
without requiring players to refill them. It runs only on the dedicated server.

## Current Behavior

- Supported native fire pieces refill their normal Valheim fuel automatically.
- Supported pieces include campfires, fire pits, bonfires, hearths, wall and
  standing torches, colored torches, braziers, Jack-o-turnips, and bathtubs.
- Existing empty supported pieces receive fuel after the server processes their
  world state and relight when Valheim's normal conditions allow it.
- The server synchronizes ordinary Valheim fuel state, so vanilla and modded
  clients see the same result without installing this mod.
- The mod creates no custom world objects or custom persistent item data.
- An empty bathtub refilled to its native capacity before and after a graceful
  dedicated-server restart.

## Player Experience

- Supported pieces look and behave like normal Valheim pieces except that
  players do not need to refuel them.
- There is no menu, keybind, custom HUD, or routine fuel notification.
- Normal environmental and placement rules still determine whether a fueled
  piece can burn.
- Unsupported pieces retain their normal Valheim behavior.

## In Development

- Confirm under normal burn conditions that every other supported piece refills
  before it visibly extinguishes during at least one refill cycle.
- Confirm after a dedicated-server restart that at least one non-bathtub piece
  still refills under normal burn conditions.
