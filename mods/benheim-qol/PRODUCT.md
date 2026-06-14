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
used from a slightly longer range.

### Portal Tag Autocomplete

When editing a portal tag:

- `Tab` cycles through matching known portal tags.
- Matching uses the text already typed as a prefix.
- If there are no prefix matches, `Tab` cycles through known loaded tags.

### Faster Portal Transition

Portal travel keeps Valheim's target-area readiness check, but shortens the
minimum distant-teleport wait from vanilla's long cinematic pause.

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
8. Interacting with stations/cauldrons works from a little farther away.
9. Portal edit `Tab` cycles known loaded tags.
10. Portal transition is faster without loading into an unready area.
11. Joining a vanilla dedicated server still works.
