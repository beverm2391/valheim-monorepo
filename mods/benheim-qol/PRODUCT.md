# Benheim

Benheim is one client mod made from small feature modules. It removes repetitive
chores without adding custom items or world objects. Most features are
client-only. Put Away uses Valheim's native chest ownership flow and does not
need a server plugin. Players without Benheim can still join and use chests.

This file owns the overall product promise. Each feature module has a
`PRODUCT.md` that owns its behavior and current test status.

## Product Rules

- Keep one client DLL and one simple install.
- Ship one idempotent installer for each desktop platform. Each installer must
  leave unrelated files and launchers unchanged. It must refuse installation
  while Valheim is running.
- Share updates as complete packages. A player updates by rerunning the
  installer. Benheim does not check GitHub or another network source for
  updates.
- The Mac launcher starts Steam when needed before it starts modded Valheim.
- The Windows installer finds Valheim across Steam libraries and creates a
  desktop shortcut. It keeps UnityDoorstop disabled for Steam Play. The shortcut
  starts Valheim with Doorstop enabled for that launch only. A player still
  needs to test the installer on a Windows PC.
- The normal Steam launch stays vanilla on Mac and Windows. `Benheim.app` on
  Mac and the `Benheim` shortcut on Windows are explicit modded launch paths.
- Keep Benheim compatible with servers and players that do not use it.
- Put Away must let Valheim's current chest owner grant the transfer. Never
  write a non-owned local chest or claim ownership as a shortcut.
- Do not add custom persistent world objects. Store a player's manual pocket
  choice on an item only when no safer representation can preserve that choice.
- Prefer normal Valheim actions over direct inventory or world mutation.
- If Valheim rejects an action, preserve vanilla behavior or explain the local
  reason without damaging game state.
- Keep controls discoverable from the native Valheim-styled Unity menu. `Left
  Shift + B` opens or closes it.
- Benheim shortcuts and modifier actions do nothing while the player edits any
  in-game text field, including portal tags and map pin names. In Benheim's
  split-stack dialog, `Backspace`, `Delete`, and `Enter` remain active as
  text-editing controls.

## Feature Modules

| Module | Product responsibility |
| --- | --- |
| [Inventory](src/Inventory/PRODUCT.md) | Split stacks, pocket items, Put Away, and hotbar loadout swap. |
| [Production](src/Production/PRODUCT.md) | Fill stations without repetitive clicks and shorten Stone Oven baking. |
| [Repair](src/Repair/PRODUCT.md) | Batch gear repair and nearby building repair. |
| [Interaction](src/Interaction/PRODUCT.md) | Less fussy interaction and station range. |
| [Portals](src/Portals/PRODUCT.md) | Faster transitions after the destination is ready. |
| [Mining](src/Mining/PRODUCT.md) | Skill-based mining damage, crits, and AOE. |
| [Woodcutting](src/Woodcutting/PRODUCT.md) | Skill-based cleave for trees and logs. |
| [Adrenaline](src/Adrenaline/PRODUCT.md) | Adrenaline gain, perfect-defense feedback, and decay timing. |
| [Farming](src/Farming/PRODUCT.md) | Mass harvesting and 5x5 grid planting. |
| [Shortcuts](src/Shortcuts/PRODUCT.md) | In-game discovery of controls and passive features. |

`Infrastructure` contains shared implementation support and has no independent
player-facing promise.

## Current Behavior

Benheim `0.1.42` is the current stable client build. Players have confirmed
installation on Mac and Windows, the native menu, and diagnostic export. Native
Put Away remains in development and will ship only after its focused two-player
gameplay test passes. Each feature module records its confirmed behavior and
remaining limits.

## In Development

Features listed under **In Development** in the module documents still need
gameplay proof or fixes.

## Later

- Craft from nearby containers with ingredient totals.
- Make mining progression configurable.
- Decide whether batch crafting should go beyond Valheim's native controls.
- Food or rested HUD only after its gameplay value justifies more UI.
