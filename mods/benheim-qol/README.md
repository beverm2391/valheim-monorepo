# BenheimQoL

Client-only Valheim quality-of-life mod for BepInEx.

Install this only on the player's machine. BenheimQoL has no server component
and does not require other players to install it.

## Features

See [`PRODUCT.md`](PRODUCT.md) for the canonical product promise and detailed
feature behavior. The in-game `F8` panel is the current shortcut reference.

## Build

Install BepInExPack Valheim locally, then run:

```bash
mods/benheim-qol/scripts/build.sh
```

If Valheim is not in the default Steam path, set:

```bash
VALHEIM_GAME_DIR="/path/to/Valheim" mods/benheim-qol/scripts/build.sh
```

## Install Locally

```bash
mods/benheim-qol/scripts/install-local.sh
```

The installer copies `BenheimQoL.dll` into:

```text
<Valheim>/BepInEx/plugins/BenheimQoL/
```

Launch Valheim through your BepInEx-enabled launcher after installing. Press
`F8` in-game to confirm the loaded version and open the shortcut reference.
