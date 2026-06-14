# BenheimQoL

Client-only Valheim quality-of-life mod for BepInEx.

The dedicated server stays vanilla. Install this only on the player's machine.

## Features

v0.1 features:

- Stack split dialog numeric typing reset, `Backspace`/`Delete` clear, and
  container-aware `Enter` transfer.
- `Left Shift` + repair click repairs all eligible gear at the current station.
- `Left Shift` + hammer repair repairs nearby damaged build pieces.
- Slightly extended interaction and crafting-station use range.
- `Tab` cycles loaded or remembered portal tags while editing a portal tag.
- Portal travel keeps the target-area readiness check but shortens the minimum
  distant-teleport wait.
- Pickaxes skill scales mining damage, crit chance, and high-skill AOE mining
  without adding bonus drops.
- `F8` toggles an in-game shortcuts panel for the whole mod, including the
  loaded plugin version.
- `Left Alt` + inventory click, or hover + `P`, pockets or unpockets an item
  type.
- `Left Alt` + `P` quick-stacks matching non-pocketed inventory items into
  nearby containers that already contain those items.

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

- Press `F8` to show or hide the BenheimQoL shortcuts panel.
- Split a stack, type a number, press `Backspace` or `Delete`, then type again.
- With a container open, split a stack and press `Enter` to move that amount to
  the other side.
- Hold `Left Shift` while pressing the station repair button.
- Hold `Left Shift` while repairing a damaged build piece with the hammer.
- Stand a little farther from a station or cauldron and open/use it normally.
- Edit a portal tag, type a prefix, then press `Tab`. Tags you see or type are
  remembered locally for future suggestions.
- Travel through a portal and compare the wait to vanilla.
- Mine rocks at different Pickaxes skill levels and watch for faster breakage,
  occasional high-skill crits, and high-skill AOE hits.
- Open inventory and hover an item, then press `P`. A small `P` marker should
  appear on matching item types. Press `P` again while hovering it to unpocket.
  `Left Alt` + click should toggle the same thing when the platform reports the
  Alt key correctly.
- Put wood/resin/etc. into nearby chests, carry matching items, and press
  `Left Alt` + `P`. Equipped items, hotbar items, and pocketed item types should
  stay in your inventory.
