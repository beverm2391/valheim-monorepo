# Spawn Protection Overlay

Show nearby base pieces' spawn-suppression coverage so players can find gaps
while building. Start with ESP's existing terrain-following rings and combined
horizontal boundary; height-aware coverage can be refined later.

## In Development

- `F8` shows or hides the overlay during gameplay. The **Spawn protection
  overlay** checkbox in Benheim Config controls the same state. The overlay
  starts off each game session.
- The menu lists the shortcut and includes it in the existing native-binding
  conflict warnings. The key is fixed; this feature needs no reassignment UI.
- Coverage comes from nearby loaded native `PlayerBase` effect areas, including
  workbenches, fires, and other pieces that carry that effect. Terrain-following
  rings hide sections inside another area's horizontal circle, exposing the
  combined boundary.
- This first version shows horizontal radius coverage. It does not promise an
  exact three-dimensional boundary on hills or around elevated pieces.
- The overlay is a local visual aid. It changes no spawn rules, server behavior,
  world objects, or saved character data.
- Acceptance needs an in-game visual check of the rings, combined boundaries,
  and synchronized key/menu controls. The candidate remains unproven until that
  check.

## Later

- Refine coverage calculations if hills or elevated pieces reveal useful
  differences between the horizontal preview and actual spawn suppression.
