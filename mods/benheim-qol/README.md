# Benheim

Benheim is an optional Valheim quality-of-life mod for BepInEx. Most features
run only on the player's computer. Multiplayer Put Away also requires the
Benheim Inventory server plugin and a compatible Benheim client for every
ready player.

## Install On A Mac

Install Valheim through Steam first. Then unzip the Mac package and double-click
`Install Benheim.command`. The installer adds the fixed BepInEx version and the
current Benheim plugin to Valheim. It creates `Benheim.app` in the user's
Applications folder.

Open `Benheim.app` to play with the mod. The launcher starts Steam when needed
and then starts the BepInEx-enabled game. The normal Steam Play button remains
the vanilla launch path.

The installer is safe to run again for an update. It refuses to run while
Valheim is open and refuses to replace an unrelated app. It also disables the
old standalone MassFarming plugin because farming is part of Benheim.

## Install On Windows

Install Valheim through Steam first. Then unzip the Windows package and
double-click `Install Benheim.cmd`. The installer finds Valheim in your
configured Steam libraries. It installs the fixed BepInEx version and the
current Benheim plugin. It also creates a `Benheim` desktop shortcut.

Open the `Benheim` desktop shortcut to play with the mod. The normal Steam Play
button remains the vanilla launch path. The installer leaves UnityDoorstop
disabled for normal Steam launches. The shortcut starts Steam when needed,
finds Valheim across configured Steam libraries, and enables Doorstop for that
launch only.

The installer stops if Valheim is open. It verifies the BepInEx download. It
does not replace an unrelated desktop shortcut. It disables the old standalone
MassFarming plugin.

## Update Benheim

Benheim does not check for updates. Get the new package from the person who
manages your server. Fully quit Valheim, unzip the package for your computer,
and run its installer again. The installer updates Benheim without removing
saves, characters, settings, or pocketed item preferences.

Press `Left Shift + B` in game to confirm the installed version and multiplayer Put Away
compatibility. The Valheim-styled Benheim menu uses Unity UI and Valheim's loaded
UI templates to show the server and dynamic ready-player version roster. Exact
versions appear for diagnosis. Put Away compatibility depends on the transaction
protocol version.

## Send A Diagnostic Log

Press `F7` in game. Benheim copies the active diagnostic log to your Desktop as
a timestamped `.txt` file and confirms the filename on screen. Attach that file
when reporting a problem. This works on both Mac and Windows while the game is
running. The log can include local paths and player or server identifiers, so
share it only with people you trust.

## Features

See [`PRODUCT.md`](PRODUCT.md) for the canonical product promise and detailed
feature behavior. The native Benheim menu is the in-game shortcut and version
reference.

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
- uploads both packages with stable asset names.

The package is written under `mods/benheim-qol/dist/`. The installer copies
`BenheimQoL.dll` into:

```text
<Valheim>/BepInEx/plugins/BenheimQoL/
```

Launch `Benheim.app` after installing. Press `Left Shift + B` in-game to confirm the
loaded version and open the native shortcut and version menu.
