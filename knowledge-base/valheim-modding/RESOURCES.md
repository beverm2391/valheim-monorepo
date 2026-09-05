# Valheim modding resources

Use this shelf to retrieve upstream source code and shipped Valheim mods that
can answer a concrete implementation question. Each entry records what the
source owns and why we would return to it.

## World migration and generation

- https://github.com/JereKuusela/valheim-upgrade_world - *Upgrade World* owns
  explored-area upgrades, regeneration, and safe-zone-aware world operations.
  Make a backup before using it for migration or administration. Return for
  Valheim migration work or a deliberate change to existing world content. Do
  not add it as a permanent dependency.
  - Shipped mod: https://thunderstore.io/c/valheim/p/JereKuusela/Upgrade_World/
  - License: https://raw.githubusercontent.com/JereKuusela/valheim-upgrade_world/main/LICENSE
    (The Unlicense)
- https://github.com/JereKuusela/valheim-expand_world_data - *Expand World
  Data* owns data-driven biome, location, vegetation, dungeon, and environment
  configuration. Return only after a deliberate Benheim decision to change
  native world generation. Existing-world changes need an explicit migration
  decision because they can alter terrain around buildings.
  - Shipped mod: https://thunderstore.io/c/valheim/p/JereKuusela/Expand_World_Data/
  - License: https://raw.githubusercontent.com/JereKuusela/valheim-expand_world_data/main/LICENSE
    (The Unlicense)

## Development and test tools

- https://github.com/JereKuusela/valheim-esp - *ESP* owns admin-client
  visualization for native mechanics such as hitboxes, attacks, resistances,
  spawn conditions, zones, structure support, smoke, and ship movement. Return
  for direct inspection during Benheim development. Keep it a local diagnostic.
  Server or administrator use requires Server Devcommands.
  - Shipped mod: https://thunderstore.io/c/valheim/p/JereKuusela/ESP/
  - License: https://raw.githubusercontent.com/JereKuusela/valheim-esp/main/LICENSE
    (The Unlicense)
- https://github.com/JereKuusela/valheim-world_edit_commands - *World Edit
  Commands* owns controlled object editing, snapshots, undo, and redo. Return
  to build and reset bounded gameplay test scenes with native objects. It can
  mutate persistent world objects. Use it only in an explicit test or admin
  workflow. Server use requires Server Devcommands.
  - Shipped mod: https://thunderstore.io/c/valheim/p/JereKuusela/World_Edit_Commands/
  - License: https://raw.githubusercontent.com/JereKuusela/valheim-world_edit_commands/main/LICENSE
    (The Unlicense)
