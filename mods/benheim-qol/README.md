# Benheim

Benheim is a client-only Valheim quality-of-life mod for BepInEx.

Install this only on the player's machine. Benheim has no server component
and does not require other players to install it.

## Install On A Mac

Install Valheim through Steam first. Then unzip the Mac package and double-click
`Install Benheim.command`. The installer adds the fixed BepInEx version and the
current Benheim plugin to Valheim. It creates `Benheim.app` in the user's
Applications folder.

Open `Benheim.app` to play. The launcher starts Steam when needed, waits for
it to become ready, and then starts the BepInEx-enabled game. The normal Steam
Play button remains the unmodded launch path.

The installer is safe to run again for an update. It refuses to run while
Valheim is open and refuses to replace an unrelated app. It also disables the
old standalone MassFarming plugin because farming is part of Benheim.

## Install On Windows

Install Valheim through Steam first. Then unzip the Windows package and
double-click `Install Benheim.cmd`. The installer finds Valheim in your
configured Steam libraries. It installs the fixed BepInEx version and the
current Benheim plugin. It also creates a `Benheim` desktop shortcut.

Open the `Benheim` desktop shortcut to play. On Windows, BepInEx loads from
the Valheim game directory, so Steam's normal Play button also starts the
modded game after installation.

You can run the installer again to update Benheim. The installer stops if
Valheim is open. It verifies the BepInEx download. It does not replace an
unrelated desktop shortcut. It disables the old standalone MassFarming plugin.

## Update Benheim

Quit Valheim, then download the latest package for your computer:

- [Latest Mac package](https://github.com/beverm2391/valheim-server/releases/latest/download/Benheim-macOS.zip)
- [Latest Windows package](https://github.com/beverm2391/valheim-server/releases/latest/download/Benheim-Windows.zip)

Unzip the package and run `Install Benheim` again. The installer replaces the
mod and launcher without removing saves, characters, settings, or pocketed item
preferences. Press `F8` in game to confirm the installed version.

## Send A Diagnostic Log

Press `F7` in game. Benheim copies the active diagnostic log to your Desktop as
a timestamped `.txt` file and confirms the filename on screen. Attach that file
when reporting a problem. This works on both Mac and Windows while the game is
running. The log can include local paths and player or server identifiers, so
share it only with people you trust.

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
mods/benheim-qol/scripts/package-windows.sh
```

Publish a tested release from a clean local `main` branch that exactly matches
`origin/main`:

```bash
mods/benheim-qol/scripts/release.sh
```

The release command:

- runs the complete client test suite;
- builds both packages;
- creates a versioned GitHub release; and
- uploads both packages with the stable asset names used by the links above.

The package is written under `mods/benheim-qol/dist/`. The installer copies
`BenheimQoL.dll` into:

```text
<Valheim>/BepInEx/plugins/BenheimQoL/
```

Launch `Benheim.app` after installing. Press `F8` in-game to confirm the
loaded version and open the shortcut reference.
