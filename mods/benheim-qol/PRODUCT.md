# Benheim

Benheim is a client-only Valheim mod made from small feature modules. It
removes repetitive chores without adding custom items, custom world data, or a
server requirement.

This file owns the overall product promise. Each feature module has a
`PRODUCT.md` that owns its behavior and current test status.

## Product Rules

- Keep one client DLL and one simple install.
- Ship one idempotent installer for each desktop platform. Each installer must
  leave unrelated files and launchers unchanged. It must refuse installation
  while Valheim is running.
- The Mac launcher starts Steam when needed before it starts modded Valheim.
- The Windows installer finds Valheim across Steam libraries and creates a
  desktop shortcut. A player still needs to test the installer on a Windows PC.
- Install a separate updater named `Update Benheim` on Mac and Windows. Normal
  game launch must never contact the network to check for Benheim updates.
- The updater must apply an update only after the stable package matches the
  checksum in `SHA256SUMS.txt`. It must preserve the existing installation if
  the release is missing or unreachable, the download is interrupted, or
  checksum verification fails.
- The updater must not replace a newer installed version with an older stable
  release.
- Keep Benheim compatible with servers and players that do not use it.
- Do not add custom persistent world objects. Store explicit per-item
  preferences in Benheim-namespaced item metadata only when another
  representation cannot preserve them safely.
- Prefer normal Valheim actions over direct inventory or world mutation.
- If Valheim rejects an action, preserve vanilla behavior or explain the local
  reason without damaging game state.
- Keep controls discoverable from the in-game shortcuts panel.

## Feature Modules

| Module | Product responsibility |
| --- | --- |
| [Inventory](src/Inventory/PRODUCT.md) | Split stacks, pocket items, and Put Away. |
| [Production](src/Production/PRODUCT.md) | Fill production and cooking stations without repetitive clicks. |
| [Repair](src/Repair/PRODUCT.md) | Batch gear repair and nearby building repair. |
| [Interaction](src/Interaction/PRODUCT.md) | Less fussy interaction and station range. |
| [Portals](src/Portals/PRODUCT.md) | Faster transitions after the destination is ready. |
| [Mining](src/Mining/PRODUCT.md) | Skill-based mining damage, crits, and AOE. |
| [Woodcutting](src/Woodcutting/PRODUCT.md) | Skill-based cleave for trees and logs. |
| [Adrenaline](src/Adrenaline/PRODUCT.md) | Perfect parry and dodge feedback, plus adrenaline decay timing. |
| [Farming](src/Farming/PRODUCT.md) | Mass harvesting and 5x5 grid planting. |
| [Shortcuts](src/Shortcuts/PRODUCT.md) | In-game discovery of controls and passive features. |

`Infrastructure` contains shared implementation support and has no independent
player-facing promise.

## Current Behavior

Benheim `0.1.13` is the latest client build that players have confirmed during
gameplay. Each feature module records its confirmed behavior.

## In Development

Features listed under **In Development** in the module documents still need
gameplay proof or fixes.

Benheim `0.1.34` is the next test build. It:

- disables the broken mass building repair action while preserving normal
  hammer repair and batch gear repair;
- shows a gold manual-pocket `P` in the top-left of an item slot and hides it
  while equipped or hotbar protection is active;
- protects every stack of a manually pocketed stackable item type, but only the
  marked instance of a non-stackable item;
- fills production station inputs and fuel, plus cooking station food and fuel,
  when the player holds `Left Shift` while interacting;
- identifies every Put Away destination by distance and direction in the
  detailed HUD receipt and shows a short generic summary above the player;
- refuses to move items with Put Away in multiplayer until Benheim can send
  each transfer to the game instance that owns the destination chest;
- moves detailed Put Away feedback to Valheim's center message area while the
  inventory is open so the inventory cannot cover it;
- shows Put Away details below the visible hotbar slots using Valheim's message
  style, without moving Valheim's own messages;
- lets players press `F7` to save a timestamped diagnostic log to the Desktop;
- uses Benheim as the player-facing name in the Mac and Windows launchers and
  in the shortcuts panel;
- includes Wood Cutting cleave for standing trees and fallen logs at skill
  level 25 or higher, with a cleave chance that rises from 30% at level 25 to
  85% at level 100 and visible `CLEAVE` combat text; and
- adds `Update Benheim` on Mac and Windows and reports when Benheim is already
  current.

## Later

- Craft from nearby containers with ingredient totals.
- Make mining progression configurable.
- Decide whether to increase adrenaline gains. The Adrenaline module changes
  only feedback and decay visibility.
- Decide whether batch crafting should go beyond Valheim's native controls.
- Food or rested HUD only after its gameplay value justifies more UI.
