#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
registration="$root/src/Farming/PlantableBerries.cs"
mass_planting="$root/src/Farming/MassPlanting.cs"
behavior="$root/tests/plantable-berries/Program.cs"

# UnityPy 1.25.0 plus TypeTreeGeneratorAPI 0.0.10 resolves the installed
# _CultivatorPieceTable.prefab in this pinned soft-reference bundle with
# m_canRemovePieces=0. Pinning the bundle makes the behavioral fixture fail
# closed when installed prefab data changes.
# shellcheck source=../scripts/valheim-source-lib.sh
source "$root/scripts/valheim-source-lib.sh"
valheim_source_resolve_assembly
valheim_data="$(dirname "$(dirname "$VALHEIM_SOURCE_ASSEMBLY_PATH")")"
softref_manifest="$valheim_data/StreamingAssets/SoftRef/manifest_extended"
cultivator_bundle="$valheim_data/StreamingAssets/SoftRef/Bundles/c4210710"
grep -Fq 'path in bundle: Assets/GameElements/Pieces/_CultivatorPieceTable.prefab' "$softref_manifest"
test "$(valheim_source_sha256_file "$cultivator_bundle")" = '2d1e17fa941213747868face6b8fb13e23332292454007255c42562119e31448'
grep -Fq 'var pieceTable = new PieceTable { m_canRemovePieces = false };' "$behavior"
grep -Fq '!toolPieces.m_canRemovePieces' "$behavior"

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
grep -Fq '[HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]' "$registration"
grep -Fq 'piece.GetCreator() == 0L' "$registration"
grep -Fq 'netView.IsOwner()' "$registration"
grep -Fq 'piece.GetCreator() == 0L' "$registration"
grep -Fq 'netView.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true);' "$registration"
grep -Fq 'internal const float BerryRespawnMinimumSeconds = 4000f;' "$registration"
grep -Fq 'internal const float BerryRespawnMaximumSeconds = 5000f;' "$registration"
grep -Fq '[HarmonyPatch(typeof(Pickable), "ShouldRespawn")]' "$registration"
grep -Fq 'PlantableBerries.TryApplyBerryRespawn(__instance);' "$registration"
grep -Fq 'IsBerryBush(pickable.gameObject)' "$registration"
grep -Fq 'zdo.GetLong(ZDOVars.s_pickedTime, 0L)' "$registration"
grep -Fq 'ResolveBerryRespawnSeconds(zdo.GetPosition(), pickedTime) / 60f' "$registration"
grep -Fq 'UnityEngine.Random.State previousState = UnityEngine.Random.state;' "$registration"
grep -Fq 'UnityEngine.Random.state = previousState;' "$registration"
if grep -Fq 'zdo.GetLong(ZDOVars.s_creator' "$registration"; then
  printf 'native berry cadence must not distinguish planted and natural bushes\n' >&2
  exit 1
fi
if grep -Fq 'zdo.m_uid' "$registration"; then
  printf 'berry cadence must not depend on ZDO IDs that world loading remaps\n' >&2
  exit 1
fi

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
grep -Fq 'piece.m_canBeRemoved = true;' "$registration"
grep -Fq 'm_amount = BerryCost,' "$registration"
grep -Fq 'm_recover = true,' "$registration"
grep -Fq '[HarmonyPatch(typeof(Piece), nameof(Piece.CanBeRemoved))]' "$registration"
grep -Fq '__result = PlantableBerries.CanRemoveBerryBush(__instance, __result);' "$registration"
grep -Fq 'nativeCanRemove && piece.GetCreator() != 0L' "$registration"
if grep -Fq 'HarmonyPatch(typeof(Player)' "$registration"; then
  printf 'berry removal must stay on the native Hammer removal path\n' >&2
  exit 1
fi

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
grep -Fq 'PlantableBerries.TryGetFootprint' "$root/src/Farming/PlantingRules.cs"
grep -Fq 'radius = footprint * 0.5f;' "$root/src/Farming/PlantingRules.cs"

# Both callers must keep using the spacing resolver and grid builder exercised
# by the behavioral fixture, so preview cannot drift from actual positions.
for caller in PlantingPreview MassPlanting; do
  grep -Fq 'PlantingRules.TryGetGridSpacing' "$root/src/Farming/$caller.cs"
  grep -Fq 'FarmingGrid.Build(' "$root/src/Farming/$caller.cs"
done

# Installed Valheim owns the persistent/network path: ZNetScene resolves the
# native prefab hash, Player clones that prefab and sets its creator, and
# Pickable stores picked time and state in the native ZDO.
grep -Fq 'm_namedPrefabs.Add(prefab.name.GetStableHashCode(), prefab);' "$source_tree/ZNetScene.cs"
grep -Fq 'return GetPrefab(name.GetStableHashCode());' "$source_tree/ZNetScene.cs"
grep -Fq 'GameObject gameObject = UnityEngine.Object.Instantiate(original, pos, rot);' "$source_tree/Player.cs"
grep -Fq 'component.SetCreator(GetPlayerID());' "$source_tree/Player.cs"
grep -Fq 'm_picked = zDO.GetBool(ZDOVars.s_picked, m_defaultPicked);' "$source_tree/Pickable.cs"
grep -Fq 'm_pickedTime = m_nview.GetZDO().GetLong(ZDOVars.s_pickedTime, 0L);' "$source_tree/Pickable.cs"
grep -Fq 'm_nview.GetZDO().Set(ZDOVars.s_picked, m_picked);' "$source_tree/Pickable.cs"
grep -Fq 'DateTime time = ZNet.instance.GetTime();' "$source_tree/Pickable.cs"
grep -Fq 'm_nview.GetZDO().Set(ZDOVars.s_pickedTime, time.Ticks);' "$source_tree/Pickable.cs"
grep -Fq 'timeSpan.TotalMinutes <= (double)m_respawnTimeMinutes' "$source_tree/Pickable.cs"
grep -Fq 'm_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", false);' "$source_tree/Pickable.cs"
grep -Fq 'if (!(m_nview == null) && m_nview.IsOwner() && GetCreator() == 0L)' "$source_tree/Piece.cs"
grep -Fq 'm_nview.GetZDO().Set(ZDOVars.s_creator, uid);' "$source_tree/Piece.cs"
grep -Fq 'return GetCreator() != 0;' "$source_tree/Piece.cs"
grep -Fq 'if (requirement.m_resItem == null || !requirement.m_recover)' "$source_tree/Piece.cs"
grep -Fq 'int dropCount = requirement.m_amount;' "$source_tree/Piece.cs"
grep -Fq 'if (!IsPlacedByPlayer())' "$source_tree/Piece.cs"
grep -Fq 'UnityEngine.Random.InitState(m_seed);' "$source_tree/Plant.cs"
grep -Fq 'return Mathf.Lerp(m_growTime, m_growTimeMax, value);' "$source_tree/Plant.cs"
grep -Fq 'm_uid.SetID(++ZDOID.m_loadID);' "$source_tree/ZDO.cs"

# Hammer removal owns authorization, the one refund call, and destruction.
# Berry provenance only narrows Piece.CanBeRemoved after those native gates.
remove_source="$source_tree/Player.cs"
removable_line="$(grep -nF 'if (!piece.m_canBeRemoved)' "$remove_source" | cut -d: -f1)"
ward_line="$(grep -nF 'if (!PrivateArea.CheckAccess(piece.transform.position))' "$remove_source" | cut -d: -f1)"
station_line="$(grep -nF 'if (!CheckCanRemovePiece(piece))' "$remove_source" | cut -d: -f1)"
piece_gate_line="$(grep -nF 'if (!piece.CanBeRemoved())' "$remove_source" | cut -d: -f1)"
claim_line="$(grep -nF 'component.ClaimOwnership();' "$remove_source" | cut -d: -f1)"
refund_line="$(grep -nF 'piece.DropResources();' "$remove_source" | cut -d: -f1)"
destroy_line="$(grep -nF 'ZNetScene.instance.Destroy(piece.gameObject);' "$remove_source" | cut -d: -f1)"
test "$removable_line" -lt "$ward_line"
test "$ward_line" -lt "$station_line"
test "$station_line" -lt "$piece_gate_line"
test "$piece_gate_line" -lt "$claim_line"
test "$claim_line" -lt "$refund_line"
test "$refund_line" -lt "$destroy_line"
test "$(grep -Fc 'piece.DropResources();' "$remove_source")" -eq 1

test "$(grep -Fc 'pickable.m_respawnTimeMinutes =' "$registration")" -eq 1
if grep -Eq 'm_defaultPicked\s*=|m_itemPrefab\s*=' "$registration"; then
  printf 'plantable berries must preserve native Pickable defaults and output\n' >&2
  exit 1
fi

# Grid resources are checked before placement and consumed only after a
# successful PlacePiece call. Every skip happens before either operation.
requirements_line="$(grep -nF 'player.HaveRequirements(anchorPiece' "$mass_planting" | cut -d: -f1)"
place_line="$(grep -nF 'player.PlacePiece(anchorPiece' "$mass_planting" | cut -d: -f1)"
consume_line="$(grep -nF 'player.ConsumeResources(anchorPiece.m_resources' "$mass_planting" | cut -d: -f1)"
test "$requirements_line" -lt "$place_line"
test "$place_line" -lt "$consume_line"
test "$(grep -Fc 'player.ConsumeResources(anchorPiece.m_resources' "$mass_planting")" -eq 1

dotnet run --project "$root/tests/plantable-berries/PlantableBerryRegistrationTests.csproj"
printf 'plantable berries source checks passed\n'
