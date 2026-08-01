# BenheimQoL

BenheimQoL is one client-only Valheim mod made from small feature modules. It
removes repetitive chores without adding custom items, custom world data, or a
server requirement.

This file owns the overall product promise. Each feature module has a
`PRODUCT.md` that owns its behavior and current test status.

## Product Rules

- Keep one client DLL and one simple install.
- Use the same idempotent Mac installer for local development and player
  installs. It must leave unrelated files and applications unchanged. It must
  refuse installation while Valheim is running.
- The Mac launcher starts Steam when needed before it starts modded Valheim.
- Keep BenheimQoL compatible with servers and players that do not use it.
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
| [Adrenaline](src/Adrenaline/PRODUCT.md) | Perfect parry and dodge feedback, plus adrenaline decay timing. |
| [Farming](src/Farming/PRODUCT.md) | Mass harvesting and 5x5 grid planting. |
| [Shortcuts](src/Shortcuts/PRODUCT.md) | In-game discovery of controls and passive features. |

`Infrastructure` contains shared implementation support and has no independent
player-facing promise.

## Current Behavior

BenheimQoL `0.1.13` is the current gameplay-confirmed client build. Each feature
module records its confirmed behavior.

## In Development

Features listed under **In Development** in the module documents still need
gameplay proof or fixes.

BenheimQoL `0.1.17` is the next test build. It includes:

- quick-stack results grouped by destination chest;
- items sorted by moved quantity within each chest;
- focused diagnostics for mass building repair;
- a shortcuts panel that preloads and groups features under Inventory, Build &
  Repair, Farming, Travel, and Combat; and
- above-player `Pocketed` or `Unpocketed` feedback after successful manual
  toggles, plus `Nothing to pocket` when `P` is pressed without a hovered
  player-inventory item.

## Later

- Craft from nearby containers with ingredient totals.
- Make mining progression configurable.
- Decide whether to increase adrenaline gains. The Adrenaline module changes
  only feedback and decay visibility.
- Decide whether batch crafting should go beyond Valheim's native controls.
- Add skill-based woodcutting effects after all planned Mining behavior is
  gameplay-confirmed.
- Food or rested HUD only after its gameplay value justifies more UI.
