# BenheimQoL

Client-only Valheim quality-of-life mod for BepInEx.

Install this only on the player's machine. BenheimQoL has no server component
and does not require other players to install it.

## Install On A Mac

Install Valheim through Steam first. Then unzip the Mac package and double-click
`Install BenheimQoL.command`. The installer adds the pinned BepInEx runtime and
the current BenheimQoL DLL to Valheim. It creates `Benheim QoL.app` in the
user's Applications folder.

Open `Benheim QoL.app` to play. The launcher starts Steam when needed, waits for
it to become ready, and then starts the BepInEx-enabled game. The normal Steam
Play button remains the unmodded launch path.

The installer is safe to run again for an update. It refuses to run while
Valheim is open and refuses to replace an unrelated app. It also disables the
old standalone MassFarming plugin because farming is part of BenheimQoL.

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

`install-local.sh` builds the DLL and invokes the same Mac installer shipped to
players. To create the shareable package, run:

```bash
mods/benheim-qol/scripts/package-macos.sh
```

The package is written under `mods/benheim-qol/dist/`. The installer copies
`BenheimQoL.dll` into:

```text
<Valheim>/BepInEx/plugins/BenheimQoL/
```

Launch `Benheim QoL.app` after installing. Press `F8` in-game to confirm the
loaded version and open the shortcut reference.
