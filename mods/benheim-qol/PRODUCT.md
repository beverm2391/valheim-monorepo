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

- The numeric amount field is focused immediately.
- The current amount is selected so typing replaces it.
- `Enter` confirms the split.
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

## Later

### Station Range Assist

Increase interaction range for crafting stations and cauldrons if Valheim treats
that range as a client-side targeting check.

### Portal Tag Autocomplete

When editing a portal tag, show known/recent tags and allow keyboard selection.

### Faster Portal Transition

Investigate whether portal travel has an artificial delay beyond real loading
and network synchronization. Only shorten artificial waiting; do not skip real
load/sync safety.

### Food And Rested HUD

Discuss mechanics and desired decisions before adding UI. Avoid HUD clutter.

## Manual Acceptance

Manual testing happens in local Valheim with BepInEx:

1. BepInEx log shows `BenheimQoL` loaded.
2. Split-stack dialog focuses the amount field and accepts `Enter`.
3. Normal repair click still repairs one item.
4. `Left Shift` + repair click repairs all eligible gear at the station.
5. Normal hammer repair still repairs one build piece.
6. `Left Shift` + hammer repair fixes damaged nearby build pieces.
7. Joining a vanilla dedicated server still works.
