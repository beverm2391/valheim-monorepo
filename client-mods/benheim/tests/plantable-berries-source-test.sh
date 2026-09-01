#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
registration="$root/src/Farming/PlantableBerries.cs"
mass_planting="$root/src/Farming/MassPlanting.cs"
product="$root/src/Farming/PRODUCT.md"

grep -Fq 'CurrentVersion { get; } = new GameVersion(0, 221, 12);' "$source_tree/Version.cs"

# The feature modifies only the three native berry prefabs. It adds build
# metadata to their existing network/pickable/destructible lifecycle.
test "$(grep -Fc 'new BerryDefinition(' "$registration")" -eq 3
grep -Fq 'new BerryDefinition("RaspberryBush"' "$registration"
grep -Fq 'new BerryDefinition("BlueberryBush"' "$registration"
grep -Fq 'new BerryDefinition("CloudberryBush"' "$registration"
grep -Fq 'internal const int BerryCost = 5;' "$registration"
grep -Fq 'scene.GetPrefab(definition.PrefabName)' "$registration"
grep -Fq 'prefab.GetComponent<ZNetView>()' "$registration"
grep -Fq 'prefab.GetComponent<Pickable>()' "$registration"
grep -Fq 'prefab.GetComponent<Destructible>()' "$registration"
grep -Fq 'pickable.m_itemPrefab?.GetComponent<ItemDrop>()' "$registration"
grep -Fq 'berry.Prefab.GetComponent<Piece>() ?? berry.Prefab.AddComponent<Piece>()' "$registration"
grep -Fq 'pieceTable.m_pieces.Add(berry.Prefab);' "$registration"

if grep -Eq 'new GameObject|Object\.Instantiate|new Plant' "$registration"; then
  printf 'plantable berries must not create a custom prefab or Plant lifecycle\n' >&2
  exit 1
fi

# Placement is ground-only. The feature does not invent cultivation, biome, or
# resource-recovery rules, and it does not expose natural bushes to removal.
grep -Fq 'piece.m_groundPiece = true;' "$registration"
grep -Fq 'piece.m_groundOnly = true;' "$registration"
grep -Fq 'piece.m_cultivatedGroundOnly = false;' "$registration"
grep -Fq 'piece.m_onlyInBiome = Heightmap.Biome.None;' "$registration"
grep -Fq 'piece.m_canBeRemoved = false;' "$registration"
grep -Fq 'm_amount = BerryCost,' "$registration"
grep -Fq 'm_recover = false,' "$registration"

# Grid spacing and collision rejection come from each native bush's collider
# shape data. Unity reports empty world-space bounds for inactive prefabs, so
# registration measures the native shapes in prefab-root space instead.
grep -Fq 'prefab.GetComponentsInChildren<Collider>(includeInactive: true)' "$registration"
if grep -Fq 'collider.bounds' "$registration"; then
  printf 'plantable berry registration must not read inactive collider bounds\n' >&2
  exit 1
fi
grep -Fq 'TryGetLocalShapeBounds(collider, out Bounds shapeBounds)' "$registration"
grep -Fq 'prefab.transform.InverseTransformPoint(' "$registration"
grep -Fq 'collider.transform.TransformPoint(localPoint)' "$registration"
grep -Fq 'Mathf.Max(footprint.size.x, footprint.size.z)' "$registration"
grep -Fq 'PlantableBerries.TryGetGridSpacing' "$root/src/Farming/PlantingRules.cs"
grep -Fq 'radius = spacing * 0.5f;' "$root/src/Farming/PlantingRules.cs"

# Installed Valheim owns the persistent/network path: ZNetScene resolves the
# native prefab hash, Player clones that prefab and sets its creator, and
# Pickable stores picked time and state in the native ZDO.
grep -Fq 'm_namedPrefabs.Add(prefab.name.GetStableHashCode(), prefab);' "$source_tree/ZNetScene.cs"
grep -Fq 'return GetPrefab(name.GetStableHashCode());' "$source_tree/ZNetScene.cs"
grep -Fq 'GameObject gameObject = UnityEngine.Object.Instantiate(original, pos, rot);' "$source_tree/Player.cs"
grep -Fq 'component.SetCreator(GetPlayerID());' "$source_tree/Player.cs"
grep -Fq 'm_picked = zDO.GetBool(ZDOVars.s_picked, m_defaultPicked);' "$source_tree/Pickable.cs"
grep -Fq 'm_pickedTime = m_nview.GetZDO().GetLong(ZDOVars.s_pickedTime, 0L);' "$source_tree/Pickable.cs"

# Grid resources are checked before placement and consumed only after a
# successful PlacePiece call. Every skip happens before either operation.
requirements_line="$(grep -nF 'player.HaveRequirements(anchorPiece' "$mass_planting" | cut -d: -f1)"
place_line="$(grep -nF 'player.PlacePiece(anchorPiece' "$mass_planting" | cut -d: -f1)"
consume_line="$(grep -nF 'player.ConsumeResources(anchorPiece.m_resources' "$mass_planting" | cut -d: -f1)"
test "$requirements_line" -lt "$place_line"
test "$place_line" -lt "$consume_line"
test "$(grep -Fc 'player.ConsumeResources(anchorPiece.m_resources' "$mass_planting")" -eq 1

grep -Fq 'Planting each bush costs five berries of its' "$product"
grep -Fq 'They do not require' "$product"
grep -Fq 'cultivated ground or a matching biome.' "$product"
grep -Fq 'each bush' "$product"
grep -Fq 'collider determines the spacing between bushes.' "$product"
grep -Fq 'Benheim adds the `Piece` component, but not the `Plant` component, to each' "$product"
grep -Fq 'Live single-player acceptance remains unproven.' "$product"
grep -Fq 'Live multiplayer acceptance remains unproven.' "$product"

dotnet run --project "$root/tests/plantable-berries/PlantableBerryRegistrationTests.csproj"
printf 'plantable berries source checks passed\n'
