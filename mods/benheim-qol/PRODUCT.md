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
- Keep Benheim compatible with servers and players that do not use it.
- Do not add custom persistent game objects or custom item data.
- Prefer normal Valheim actions over direct inventory or world mutation.
- If Valheim rejects an action, preserve vanilla behavior or explain the local
  reason without damaging game state.
- Keep controls discoverable from the in-game shortcuts panel.

## Feature Modules

| Module | Product responsibility |
| --- | --- |
| [Inventory](src/Inventory/PRODUCT.md) | Split stacks, pocket items, and quick stack. |
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

Benheim `0.1.26` is the next test build. It:

- disables the broken mass building repair action while preserving normal
  hammer repair and batch gear repair;
- moves the manual and automatic protection markers to the bottom-left corner
  of each item slot;
- identifies every Put Away destination by distance and direction and shows a
  floating item receipt above each chest that received items;
- moves detailed Put Away feedback to Valheim's center message area while the
  inventory is open so the inventory cannot cover it;
- uses Benheim as the player-facing name in the Mac and Windows launchers and
  in the shortcuts panel; and
- includes Wood Cutting cleave for standing trees and fallen logs at skill
  level 25 or higher, with a cleave chance that rises from 15% at level 25 to
  45% at level 100 and visible `CLEAVE` combat text.

## Later

- Craft from nearby containers with ingredient totals.
- Make mining progression configurable.
- Decide whether to increase adrenaline gains. The Adrenaline module changes
  only feedback and decay visibility.
- Decide whether batch crafting should go beyond Valheim's native controls.
- Food or rested HUD only after its gameplay value justifies more UI.
