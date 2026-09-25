# Benheim Building

Make ambitious Valheim buildings easier and more expressive through useful
pieces and construction tools. We will build our own version and borrow what
works from community mods. The ideas below are not a settled feature list or
implementation plan.

## Ideas to Explore

- Snapping and rotation that make angled layouts and awkward connections easy
  to place accurately.
- Simple, reusable shapes: triangular and partial floors, roof pieces that
  close angled or hexagonal layouts, and matching walls and beams.
- Previewing and placing repeated rows, grids, and larger sections without
  clicking each piece individually.
- Capturing and reusing building patterns or blueprints.
- A better viewpoint for tall and intricate work, potentially a flying build
  camera near a workbench. The method is open; easier building is the goal.
- Clear previews and a way to recover from large accidental placements.

The exact piece set, controls, blueprint behavior, costs, and multiplayer
behavior remain open. [Benheim Terrain](../benheim-terrain/PRODUCT.md) owns the
separate terrain-shaping direction.

## Community Source Pointers

These are starting points for studying behavior and implementation. Check the
current license and the rights to any assets before copying source or models.

- [MissingPieces source](https://github.com/OrianaVenture/Valheim-MissingPieces)
  (WTFPL): triangular floors and diagonal roof edges.
- [PlanBuild source](https://github.com/sirskunkalot/PlanBuild) (WTFPL):
  planning, blueprint selection, capture, and reuse.
- [ComfyGizmo source](https://github.com/BruceOfTheBow/BruceComfyMods/tree/main/ComfyGizmo)
  (GPL-3.0): multi-axis and adjustable-step rotation.
- [Extra Snap Points Made Easy source](https://github.com/searica/ExtraSnapPointsMadeEasy)
  (GPL-3.0): snap-point selection and added anchors.
- [Build Camera source](https://github.com/gittywithexcitement/ValheimBuildCamera)
  (GPL-3.0; deprecated mod): detached building viewpoint.
- [SmartBuild mod page](https://thunderstore.io/c/valheim/p/codepeople/SmartBuild/):
  rows, grids, arcs, and repeated placement; source and reuse terms still need
  locating.
