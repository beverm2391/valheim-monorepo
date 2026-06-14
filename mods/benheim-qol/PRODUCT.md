# BenheimQoL Product Spec

BenheimQoL is a client-only Valheim quality-of-life mod for BepInEx. It removes
small UI chores without adding custom items, custom world data, or server
requirements. The dedicated server remains vanilla.

## Product Rules

- Client-only by default. The server does not install this mod.
- Public-clean. No private server names, passwords, IPs, or personal paths in
  source or docs.
- No custom persistent game objects or custom item data.
- Prefer normal Valheim actions over direct inventory/world mutation.
- If Valheim rejects an action, fail quietly and leave vanilla behavior intact.

## v0.1 Scope

### Stack Split Autofocus

When the split-stack dialog opens:

- Numeric typing is primed immediately.
- The first number typed after opening replaces the previous typed amount.
- `Backspace` or `Delete` clears the typed amount back to `1`.
- `Enter` confirms the split.
- If a container is open, `Enter` moves the split amount to the opposite
  inventory instead of leaving the split stack on the cursor.
- Existing cancel behavior remains unchanged.

### Shift-Click Repair All Gear

At a valid repair-capable crafting station:

- Normal repair click keeps vanilla one-item repair behavior.
- Holding `Left Shift` while clicking repair repairs all repairable gear that
  the current station can repair.
- No remote repair. The player still needs the correct station context.

### Shift-Click Mass Building Repair

With the hammer in repair mode:

- Normal repair click keeps vanilla one-piece repair behavior.
- Holding `Left Shift` while repairing one piece repairs damaged nearby build
  pieces in the relevant build/workbench radius.
- The feature should use the longest range that still feels like the player's
  current building station area, not the entire loaded world.

### Extended Interaction Range

Crafting stations, cauldrons, portals, and other normal interact targets can be
used from a slightly longer range. Crafting stations also get their own use
distance increased so food crafting/crafting UI checks are not limited by
vanilla's shorter station distance.

### Portal Tag Autocomplete

When editing a portal tag:

- `Tab` cycles through matching known portal tags.
- Matching uses the text already typed as a prefix.
- If there are no prefix matches, `Tab` cycles through known loaded tags.
- Seen or typed tags are remembered in a local BepInEx config file for future
  autocomplete attempts.

### Faster Portal Transition

Portal travel keeps Valheim's target-area readiness check, but shortens the
minimum distant-teleport wait from vanilla's long cinematic pause.

### Pickaxes Progression

Mining gets skill-based quality-of-life progression without extra drops:

- Pickaxe damage scales gently with Pickaxes skill.
- Mining crit chance unlocks after Pickaxes 25 and scales up from there.
- AOE mining unlocks after Pickaxes 50 and scales in radius up to Pickaxes 100.
- AOE mining applies reduced pickaxe damage to nearby mine rocks without
  multiplying drops directly.

### Pocket Items And Quick Stack

Inventory cleanup uses one player-facing concept: pocketed items stay with you.

- Equipped gear is always pocketed.
- Hotbar/top-row items are always pocketed.
- `Left Alt` + inventory click, or hover + `P`, toggles manual pocketing for
  that item type.
- Manually pocketed item types show a small `P` marker in the player inventory.
- The pocket list is local BepInEx config and persists between launches.
- `Left Alt` + `Q` quick-stacks matching non-pocketed inventory items into
  nearby accessible containers.
- Quick stack only deposits into containers that already contain the item and
  have room for more of it.
- Quick stack does not add custom item data or require the server to install the
  mod.

## Later

### Station Range Assist

Tune interaction range after manual testing.

### Portal Tag Autocomplete

Improve beyond loaded-tag `Tab` cycling if the simple helper feels useful.

### Faster Portal Transition

Tune the shortened minimum delay after testing.

### Food And Rested HUD

Discuss mechanics and desired decisions before adding UI. Avoid HUD clutter.

## Manual Acceptance

Manual testing happens in local Valheim with BepInEx:

1. BepInEx log shows `BenheimQoL` loaded.
2. Split-stack dialog accepts typing, `Backspace`/`Delete`, and `Enter`.
3. With a container open, split `Enter` moves the amount to the opposite side.
4. Normal repair click still repairs one item.
5. `Left Shift` + repair click repairs all eligible gear at the station.
6. Normal hammer repair still repairs one build piece.
7. `Left Shift` + hammer repair fixes damaged nearby build pieces.
8. Interacting with stations/cauldrons and opening food crafting works from a
   little farther away.
9. Portal edit `Tab` cycles loaded or remembered tags.
10. Portal transition is faster without loading into an unready area.
11. Mining feels faster as Pickaxes skill rises.
12. At higher Pickaxes skill, mining can crit and damage nearby mine rocks.
13. Hover + `P` pockets/unpockets an item type and shows or hides the `P`
    marker. `Left Alt` + inventory click should do the same when the platform
    reports the Alt key correctly.
14. `Left Alt` + `Q` moves matching non-pocketed items into nearby containers.
15. Equipped items, hotbar items, and manually pocketed item types stay in the
    player inventory during quick stack.
16. Joining a vanilla dedicated server still works.
