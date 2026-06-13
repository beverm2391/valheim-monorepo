# BenheimQoL

Client-only Valheim quality-of-life mod for BepInEx.

The dedicated server stays vanilla. Install this only on the player's machine.

## Features

v0.1 features:

- Stack split dialog numeric typing reset and `Enter` confirm.
- `Left Shift` + repair click repairs all eligible gear at the current station.
- `Left Shift` + hammer repair repairs nearby damaged build pieces.

See [`PRODUCT.md`](PRODUCT.md) for the product contract and manual acceptance
checks.

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

Launch Valheim through your BepInEx-enabled launcher after installing. In-game,
test with:

- Split a stack, type a number, then press `Enter`.
- Hold `Left Shift` while pressing the station repair button.
- Hold `Left Shift` while repairing a damaged build piece with the hammer.
