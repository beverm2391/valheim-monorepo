# BenheimQoL Product Reference

BenheimQoL is a client-only Valheim quality-of-life mod for BepInEx. It removes
small chores from normal play without adding custom items, custom world data, or
server requirements. Dedicated servers can remain vanilla.

This document is the canonical product reference for BenheimQoL. Update it when
a feature is added, removed, renamed, or materially changes behavior.

## Product Rules

- Client-only by default. Servers do not install this mod.
- Public-clean. No private server names, passwords, IPs, save paths, or personal
  identifiers in source or docs.
- No custom persistent game objects or custom item data.
- Prefer normal Valheim actions over direct inventory/world mutation.
- If Valheim rejects an action, fail quietly or explain the local reason without
  damaging vanilla behavior.
- Keep controls discoverable from the in-game shortcuts panel.

## Shortcut Reference

| Shortcut | Context | Behavior |
| --- | --- | --- |
| `F8` | Anywhere outside text/console input | Show or hide the BenheimQoL shortcuts panel. |
| Hover + `P` | Player inventory | Pocket or unpocket the hovered item type. |
| `Left Alt` + inventory click | Player inventory | Pocket or unpocket the clicked item type, when platform input reports Alt correctly. |
| `Left Alt` + `P` | Inventory open | Quick stack matching non-pocketed items into nearby containers. |
| `Backspace` / `Delete` | Split-stack dialog | Clear the split amount back to `1`. |
| `Enter` | Split-stack dialog | Confirm split; with a container open, move the split amount to the opposite inventory. |
| `Left Shift` + station repair click | Repair-capable crafting station | Repair all eligible gear for the current station. |
| `Left Shift` + hammer repair click | Hammer repair mode | Repair nearby damaged building pieces. |
| `Tab` | Portal tag edit dialog | Cycle known portal tag matches. |

## Feature Reference

### Shortcuts Panel

The shortcuts panel is the mod's discoverability surface.

- `F8` toggles the panel.
- The title shows the loaded BenheimQoL version.
- The panel lists active shortcuts and passive features.
- The panel should be readable over gameplay and avoid the hotbar/minimap.
- The panel stores no world data and requires no server support.

### Stack Split Improvements

The split-stack dialog is made keyboard-friendly.

- Numeric typing is primed immediately when the dialog opens.
- The first number typed after opening replaces the previous typed amount.
- `Backspace` or `Delete` clears the amount back to `1`.
- `Enter` confirms the split.
- If a container is open, `Enter` moves the split amount to the opposite
  inventory instead of leaving the split stack on the cursor.
- Existing cancel behavior remains vanilla.

### Repair All Gear

Station repair gets a batch path while preserving vanilla one-click repair.

- Normal repair click repairs one eligible item, as in vanilla.
- `Left Shift` + station repair click repairs all repairable gear the current
  station can repair.
- The player still needs the correct station context.
- The feature does not remotely repair gear away from the proper station.

### Mass Building Repair

Hammer repair gets a nearby-area batch path.

- Normal hammer repair click repairs one build piece, as in vanilla.
- With the hammer in repair mode, `Left Shift` + repair click scans nearby build
  pieces around the hovered anchor piece.
- Damaged accessible pieces in range are repaired up to the per-click cap.
- If the mod catches the shortcut but finds no damaged pieces, it reports that
  no damaged build pieces were found nearby.
- The feature is intended to feel like repairing the current workbench/building
  area, not the entire loaded world.

### Extended Interaction Range

A few interaction checks are made less fussy.

- General player interaction range is extended modestly.
- Crafting station use distance is extended so cauldrons/workbenches/forges can
  be opened and used from a little farther away.
- This is a convenience feature, not remote crafting from across a base.

### Portal Tag Autocomplete

Portal naming gets lightweight autocomplete.

- When editing a portal tag, `Tab` cycles matching known portal tags.
- Matching uses the text already typed as a prefix.
- If there are no prefix matches, `Tab` cycles known loaded tags.
- Seen or typed portal tags are remembered in a local BepInEx config file for
  future autocomplete attempts.
- Tags are local client convenience data, not world/server data.

### Faster Portal Transition

Portal travel keeps Valheim's target-area readiness check while shortening the
minimum distant-teleport wait.

- The mod does not skip the readiness check.
- The goal is to remove artificial waiting after the destination is ready, not
  load the player into an unsafe/unready area.

### Pickaxes Progression

Mining gets skill-based quality-of-life progression without extra drops.

- Pickaxe damage scales gently with Pickaxes skill.
- Mining crit chance unlocks after Pickaxes 25 and scales from there.
- AOE mining unlocks after Pickaxes 25 and scales in radius up to Pickaxes 100.
- AOE mining applies reduced pickaxe damage to nearby mine-rock hit areas.
- The feature does not directly multiply drops or add bonus loot.

### Adrenaline Feedback

When the player has an adrenaline meter, successful perfect defensive actions
show a local yellow popup.

- A successful perfect parry shows a yellow `PARRY` popup.
- A successful perfect dodge shows a yellow `PERFECT DODGE` popup.
- The popup includes the adrenaline awarded after Valheim's active world and
  status-effect multipliers, plus the resulting meter total and maximum.
- When the equipped adrenaline trinket activates, the popup says `ACTIVATED`
  instead of showing the reset meter value.
- Ordinary blocks and ordinary rolls do not show these popups.
- The feature only reports adrenaline Valheim actually awards. It does not
  change gain amounts, decay, timing windows, or any other balance.

### Pocket Items

Inventory cleanup uses one player-facing concept: pocketed items stay with you.

- Equipped gear is always pocketed.
- Hotbar/top-row items are always pocketed.
- Hover + `P` toggles manual pocketing for the hovered item type.
- `Left Alt` + inventory click toggles manual pocketing when platform input
  reports Alt correctly.
- Manual pocketing applies by item type, not by individual stack.
- Manually pocketed item types show a small `P` marker in the player inventory.
- The pocket list is stored in local BepInEx config and persists between
  launches.

### Quick Stack

Quick stack stows non-pocketed inventory items into nearby matching containers.

- `Left Alt` + `P` runs quick stack while inventory is open.
- It scans nearby accessible containers.
- It only deposits into containers that already contain the same item and have
  room for more of it.
- It skips equipped gear, hotbar/top-row items, and manually pocketed item
  types.
- It does not invent container categories or move items into empty chests.
- If nothing moves, the message should explain why, such as pocketed/hotbar,
  no matching chest, full chest, or busy chest.

## Later

Potential future work:

- Craft from nearby containers with ingredient totals.
- Better portal tag selector/dropdown UI.
- Config-driven tuning for mining, range, and quick stack radius.
- Food/rested HUD only after deciding the gameplay value is worth the UI.
