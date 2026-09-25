# Benheim Terrain

Make it easier to prepare a site and shape the land around a build. We will
build our own terrain mod, learning from useful behavior in community mods.
The toolset and controls remain open.

## Ideas to Explore

- Level, raise, lower, and smooth larger areas with a preview of the affected
  ground before applying a change.
- Useful shapes and sizes for foundations, slopes, paths, and landscaping.
- Precise height targets and alignment with building pieces where they help.
- A way to restore or undo terrain edits that went further than intended.

The existing Hoe radius action belongs to [Benheim Farming](../benheim/src/Farming/PRODUCT.md).
How these tools relate to that action, what they cost, and how they work in the
shared world remain open. [Benheim Building](../benheim-building/PRODUCT.md)
owns construction pieces, placement, and reusable structures.

## Community Source Pointers

These are starting points for studying behavior and implementation. Check the
current license before copying source.

- [PlanBuild source](https://github.com/sirskunkalot/PlanBuild) (WTFPL):
  area flattening and terrain markers attached to blueprints.
- [VentureValheim source](https://github.com/OrianaVenture/VentureValheim)
  (MIT): includes TerrainReset.
- [TerrainTools source](https://github.com/searica/TerrainTools) (GPL-3.0):
  terrain shaping and recovery mechanisms.
- [TerrainShaperPlus mod page](https://thunderstore.io/c/valheim/p/PONEIS/TerrainShaperPlus/):
  variable-area brushes, height controls, and smoothing; source and reuse terms
  still need locating.
