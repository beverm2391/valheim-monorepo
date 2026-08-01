# BenheimQoL 0.1.11 Stabilization Pass

This is the one-time checklist for the current stabilization pass. It is not a
product contract or a permanent source of truth. Product behavior remains owned
by [`PRODUCT.md`](PRODUCT.md). Delete this file when the pass is closed.

Install the current DLL, fully quit Valheim, and relaunch through the
BepInEx-enabled launcher before starting. Record failures with the shortcut or
action used, the expected result, the observed result, and any on-screen
message.

| Area | Action | Expected result |
| --- | --- | --- |
| Load | Open the `F8` panel. | BenheimQoL loads and the panel shows version `0.1.11`. |
| Shortcuts panel | Press `F8` twice. | The panel appears and disappears; text is readable over gameplay. |
| Split stack | Open the split dialog, type a number, delete it, type again, and press `Enter`. | The amount resets cleanly and confirms. |
| Split transfer | Open a container, split from either side, and press `Enter`. | The split amount moves to the opposite inventory when space exists. |
| Gear repair | Use the normal station repair click. | Vanilla one-item repair still works. |
| Gear repair all | Hold `Left Shift` and click station repair. | All eligible gear at that station repairs. |
| Building repair | Use hammer repair without Shift. | Vanilla one-piece repair still works. |
| Building mass repair | Damage nearby pieces, then hold `Left Shift` and repair one with the hammer. | Nearby damaged pieces repair; if none qualify, the mod says so. |
| Interaction range | Use a station or cauldron from slightly beyond vanilla range. | It opens and remains usable. |
| Portal autocomplete | Edit a portal tag, type a prefix, and press `Tab`. | Known or remembered matching tags cycle. |
| Portal transition | Travel through a portal. | The transition is faster but still waits for destination readiness. |
| Mining progression | Mine after Pickaxes 25. | Higher skill improves mining and nearby hit areas receive visible AOE damage. |
| Perfect parry feedback | With an adrenaline trinket equipped, successfully parry an enemy, then hold an ordinary block. | Yellow `Perfect parry +N` text appears above the player; the ordinary block shows nothing. |
| Perfect dodge feedback | With an adrenaline trinket equipped, perfect-dodge an incoming attack, then perform an ordinary roll. | Yellow `Perfect dodge +N` text appears above the player; the ordinary roll shows nothing. |
| Adrenaline decay HUD | Gain some adrenaline and stop gaining more until it reaches zero. | A small left-aligned line directly under the native meter counts down as `Decay <time>s`, switches to `Decaying <time>s`, reaches zero with the meter, then disappears. |
| Full adrenaline | Fill the adrenaline meter and trigger the equipped trinket. | The native meter value and full-meter effect behave exactly as before. |
| Pocket item | Hover a player-inventory item and press `P`. | Its item type gains or loses the `P` marker and the status message agrees. |
| Quick stack | Put resin in a nearby chest, carry resin outside the hotbar, and press `Left Alt` + `P`. | Resin moves into the matching chest. |
| Quick stack protection | Put a matching item in the hotbar or pocket it, then quick stack. | The item stays with the player and any no-op message explains why. |
| Multiplayer compatibility | Join the dedicated server with BenheimQoL enabled. | The client connects and normal shared-world behavior still works. |
