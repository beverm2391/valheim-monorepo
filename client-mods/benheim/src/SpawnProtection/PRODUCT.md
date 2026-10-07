# Spawn Protection Overlay

A local developer view for base planning: show nearby base pieces'
spawn-suppression coverage so players can find gaps. Use ESP's existing
terrain-following rings and combined horizontal boundary for the first version.

## Current Behavior

- Ben accepted plain bright-green rings with covered sections clipped in the
  local Lab preview. The rings have no shaded fill or dimmed interior arcs.
- Ben accepted grey `[no_spawn]` text beneath the minimap's danger level in the
  local Lab preview. The label uses exactly that spelling and brackets.

## In Development

- Ben's server-world test failed: the overlay was not visible and the game
  nearly froze. Visibility and dense-base responsiveness require a fix and
  proof before another gameplay acceptance pass. The cause is not established.
- `F8` shows or hides both the rings and minimap indicator during gameplay. The
  **Spawn protection overlay** checkbox in Benheim Config controls that same
  state. The view starts off each game session.
- The menu lists the shortcut and includes it in the existing native-binding
  conflict warnings. The key is fixed; this feature needs no reassignment UI.
- Coverage comes from nearby loaded native `PlayerBase` effect areas, including
  workbenches, fires, and other pieces that carry that effect. Terrain-following
  rings hide sections inside another area's horizontal circle, exposing the
  combined boundary.
- Spawn-coverage rings are bright green so players can distinguish them from
  Valheim's native workbench range markers.
- While the view is on, the accepted minimap label appears when Valheim's
  native `PlayerBase` check includes the local player's position. It hides
  outside that coverage or when the view is off.
- This first version shows horizontal radius coverage. It does not promise an
  exact three-dimensional boundary on hills or around elevated pieces.
- The overlay is a local visual aid. It changes no spawn rules, server behavior,
  world objects, or saved character data.
- Installed physical-key and menu synchronization, indicator behavior across
  coverage boundaries, and responsiveness around a real dense base remain
  unproven. Accepted Lab appearance does not establish those results.

## Later

- Refine coverage calculations if hills or elevated pieces reveal useful
  differences between the horizontal preview and actual spawn suppression.
