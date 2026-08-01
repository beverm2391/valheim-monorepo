# BenheimQoL

BenheimQoL is one client-only Valheim mod made from small feature modules. It
removes repetitive chores without adding custom items, custom world data, or a
server requirement.

This file owns the overall product promise. Each feature module has a
`PRODUCT.md` that owns its behavior and current test status.

## Product Rules

- Keep one client DLL and one simple install.
- Keep servers and players without BenheimQoL compatible.
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
| [Portals](src/Portals/PRODUCT.md) | Portal tag autocomplete and faster transitions. |
| [Mining](src/Mining/PRODUCT.md) | Skill-based mining damage, crits, and AOE. |
| [Adrenaline](src/Adrenaline/PRODUCT.md) | Perfect-defense feedback and decay timing. |
| [Shortcuts](src/Shortcuts/PRODUCT.md) | In-game discovery of controls and passive features. |

`Infrastructure` contains shared implementation support and has no independent
player-facing promise.

## Current Status

The reorganized client DLL builds successfully and still includes every
existing game patch. It has not been installed. Every module needs an in-game
check on this build, and features marked **In development** still require fixes.

## Later

- Craft from nearby containers with ingredient totals.
- Better portal tag selector or dropdown UI.
- Config-driven tuning for mining, range, and quick-stack radius.
- Food or rested HUD only after its gameplay value justifies more UI.
