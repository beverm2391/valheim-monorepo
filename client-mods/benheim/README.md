# Benheim

Benheim is a curated Valheim gameplay mod for BepInEx. Its current features run
on the player's computer. Our regular group must install and run compatible
Benheim versions. Group packages include Axiom diagnostics configuration and
are shared privately by the group operator; there is no public mod release.

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

Press `Left Shift + B` in game to confirm the installed version and review the
controls and diagnostics delivery status. An installer verifies the DLL and
diagnostics configuration on disk. Installation is complete only after a fresh
event from that build is received in Axiom; the installer cannot establish
that receipt without running the game.

## Send A Diagnostic Log

Each managed Mac or Windows modded launch archives the full
`BepInEx/LogOutput.log` from the previous run before BepInEx starts. Benheim
keeps the 10 newest archives it created in `BepInEx/BenheimLogArchive` and the
current active log. If the previous run crashed, the next managed launch
archives the leftover log too. If archiving fails, the launcher shows a visible
warning that does not block the managed launch. Normal Steam launches remain
vanilla and do not run this archive step.

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
client-mods/benheim/scripts/verify.sh
```

`verify.sh` runs all client source and installer checks, the Put Away summary
checks, and the Release DLL build. It does not install, package, or publish
anything. To build only the DLL, run:

```bash
client-mods/benheim/scripts/build.sh
```

If Valheim is not in the default Steam path, set:

```bash
VALHEIM_GAME_DIR="/path/to/Valheim" client-mods/benheim/scripts/verify.sh
```

## Install Locally

```bash
client-mods/benheim/scripts/install-local.sh
```

`install-local.sh` builds the DLL and invokes the same Mac installer shipped to
players. Set `BENHEIM_QOL_PRIVATE_DIAGNOSTICS_FILE` to a local Axiom
configuration file whose build ID matches that DLL. To install one
already-built group package and verify that its exact version, DLL bytes, and
diagnostics configuration landed, run:

```bash
client-mods/benheim/scripts/install-local.sh --package /path/to/Benheim-macOS-X.Y.Z.zip
```

Both paths use `client-mods/benheim/scripts/check-valheim-stopped.sh` for the
same exact-process safety gate. To create private Mac and Windows group
packages, inject a dataset-scoped Axiom ingest token through the process
environment and run:

```bash
client-mods/benheim/scripts/package-all.sh
```

`package-all.sh` runs `verify.sh` once, then creates both packages from the
same verified Release DLL. The Benheim source must be committed, but unrelated
work elsewhere in the shared branch does not block packaging. Packages stay
local and must be shared only with the intended group. Rotate the ingest token
if a package leaves that group.

The packages are written under `client-mods/benheim/dist/`. The installer copies
`BenheimQoL.dll` into:

```text
<Valheim>/BepInEx/plugins/BenheimQoL/
```

Launch `Benheim.app` after installing. Press `Left Shift + B` in-game to confirm the
loaded version and open the native shortcut and version menu.
