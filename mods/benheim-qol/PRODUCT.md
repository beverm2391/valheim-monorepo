# Benheim

Benheim is one client mod made from small feature modules. It removes repetitive
chores without adding custom items or world objects. Most features are
client-only. Put Away uses Valheim's native chest ownership flow and does not
need a server plugin. Players without Benheim can still join and use chests.

This file owns the overall product promise. Each feature module has a
`PRODUCT.md` that owns its behavior and proof status.

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
- On each managed Mac or Windows modded launch, the launcher archives the full
  `BepInEx/LogOutput.log` from the previous run before BepInEx starts. The
  launcher keeps the 10 newest archives that Benheim created and the current
  active log. If the previous run crashed, the next managed launch archives
  the leftover log. If archiving fails, the launcher shows a visible warning
  that does not block the managed launch.
- `F7` remains the manual way to export the active log for sharing.
- Keep Benheim compatible with servers and players that do not use it.
- For features that depend on the zone owner, every client in the expected
  playgroup must run the same Benheim version for consistent behavior. Vanilla
  clients may still join, but zones they own use Valheim's native behavior for
  those features.
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
| [Archery](src/Archery/PRODUCT.md) | Global arrow headshots and collision-time feedback. |
| [Farming](src/Farming/PRODUCT.md) | Mass harvesting and 5x5 grid planting. |
| [Spawning](src/Spawning/PRODUCT.md) | Adjust spawn opportunities for selected native creatures. |
| [Shortcuts](src/Shortcuts/PRODUCT.md) | In-game discovery of controls and passive features. |

`Infrastructure` contains shared implementation support and has no independent
player-facing promise.

## Current Behavior

Benheim `0.1.52` is the current local gameplay candidate. Players have confirmed
installation on Mac and Windows, the native menu, diagnostic export, Mass
Repair, and doubled adrenaline with its native decay delay. Ben confirmed
global Bow headshots on a Berserker and a Shaman in `0.1.49`. Ben confirmed the
`0.1.49` solo Put Away behavior except for its receipt placement. Benheim Put
Away remains in development until the new grouped-receipt placement passes
retest and the focused two-player gameplay test passes. Each feature module
records its confirmed behavior and remaining limits.

## In Development

Features listed under **In Development** in the module documents still need
gameplay proof or fixes.

All Benheim feedback that belongs in the top-left area shares one Benheim-owned
lane directly beneath the live hotbar. The lane carries grouped receipts for
Put Away and Mass Repair, pocket and unpocket confirmations, and Put Away's
already-in-progress message. It never moves to the hotbar's right side or into
a separate right-side column. When Valheim shows visible native top-left status
text, the lane moves farther down to avoid overlap while staying directly
beneath the live hotbar and on screen. It never intercepts or restyles
Valheim's messages. Short confirmations do not replace an active Put Away or
Mass Repair grouped receipt. Center messages and world feedback keep their
existing UI locations. This unified lane still needs gameplay proof.

If Benheim cannot attach a required gameplay hook, it disables all Benheim
gameplay actions. It logs the exact failure and a `[diag][Health]` event. It
keeps the problem visible in the menu Warnings block and shows one prominent
message per session that directs the player to `Left Shift + B`. If only
keybind inspection fails, Benheim reports the problem in logs and Warnings
without interrupting unrelated gameplay.

## Later

- Craft from nearby containers with ingredient totals.
- Make mining progression configurable.
- Decide whether batch crafting should go beyond Valheim's native controls.
- Food or rested HUD only after its gameplay value justifies more UI.
