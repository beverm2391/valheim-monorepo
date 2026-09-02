using System;
using BenheimQoL;
using BenheimQoL.Farming;
using BenheimQoL.Infrastructure;
using UnityEngine;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void RequireNear(float actual, float expected, string message)
{
    if (Math.Abs(actual - expected) > 0.0001f)
    {
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}

static GameObject CreateBerryItem(string name)
{
    var item = new GameObject(name);
    item.AddComponent<ItemDrop>().m_itemData.Icon = new Sprite();
    return item;
}

static GameObject CreateBush(string name, GameObject berryItem)
{
    var bush = new GameObject(name);
    bush.AddComponent<ZNetView>();
    bush.AddComponent<Destructible>();
    Pickable pickable = bush.AddComponent<Pickable>();
    pickable.m_itemPrefab = berryItem;
    pickable.m_respawnTimeMinutes = 300f;
    return bush;
}

static (bool Removed, int RefundAmount, string? RefundItem) TryNativeHammerRemoval(
    Piece piece,
    long removingPlayerId,
    PieceTable toolPieces,
    bool wardAccess)
{
    _ = removingPlayerId;
    if (!toolPieces.m_canRemovePieces || !piece.m_canBeRemoved || !wardAccess || piece.Removed)
    {
        return (false, 0, null);
    }

    bool canRemove = piece.CanBeRemoved();
    PlantableBerryRemovalPatch.Postfix(piece, ref canRemove);
    if (!canRemove)
    {
        return (false, 0, null);
    }

    (int amount, string? itemName) = piece.DropResources();
    piece.Destroy();
    return (true, amount, itemName);
}

static SphereCollider AddSphere(GameObject parent, string name, float radius, Vector3 position, Vector3 scale)
{
    var child = new GameObject(name);
    child.transform.localPosition = position;
    child.transform.localScale = scale;
    parent.AddChild(child);
    var collider = child.AddComponent<SphereCollider>();
    collider.radius = radius;
    return collider;
}

static CapsuleCollider AddCapsule(GameObject parent, string name, float radius, float height)
{
    var child = new GameObject(name);
    parent.AddChild(child);
    var collider = child.AddComponent<CapsuleCollider>();
    collider.center = new Vector3(0f, 0.36312f, 0f);
    collider.radius = radius;
    collider.height = height;
    collider.direction = 1;
    return collider;
}

var pieceTable = new PieceTable { m_canRemovePieces = false };
var hammerPieceTable = new PieceTable { m_canRemovePieces = true };
var nativePlant = new GameObject("sapling_carrot");
nativePlant.AddComponent<Plant>();
nativePlant.AddComponent<Piece>().m_placeEffect = new EffectList();
pieceTable.m_pieces.Add(nativePlant);

var cultivator = new GameObject("Cultivator");
cultivator.AddComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces = pieceTable;
ObjectDB.instance = new ObjectDB();
ObjectDB.instance.AddItemPrefab(cultivator);

var scene = new ZNetScene();

GameObject raspberry = CreateBush("RaspberryBush", CreateBerryItem("Raspberry"));
AddCapsule(raspberry, "model", 0.2f, 0.9062346f);
AddSphere(raspberry, "Sphere", 0.5f, new Vector3(0f, 0.502f, 0f), Vector3.one);
AddSphere(raspberry, "viewblock", 0.71f, new Vector3(0f, 0.54f, 0f), Vector3.one);
raspberry.SetActive(false);
scene.AddPrefab(raspberry);

GameObject blueberry = CreateBush("BlueberryBush", CreateBerryItem("Blueberries"));
AddCapsule(blueberry, "model", 0.2f, 0.9062346f);
AddSphere(blueberry, "Sphere", 0.5f, new Vector3(0f, 0.502f, 0f), Vector3.one);
AddSphere(blueberry, "viewblock", 0.8f, new Vector3(0f, 0.455f, 0f), Vector3.one);
blueberry.SetActive(false);
scene.AddPrefab(blueberry);

GameObject cloudberry = CreateBush("CloudberryBush", CreateBerryItem("Cloudberry"));
AddSphere(
    cloudberry,
    "Sphere",
    0.5f,
    new Vector3(0f, 0.393f, 0f),
    new Vector3(0.73952f, 0.73952f, 0.73952f));
cloudberry.SetActive(false);
scene.AddPrefab(cloudberry);

foreach (GameObject bush in new[] { raspberry, blueberry, cloudberry })
{
    foreach (Collider collider in bush.GetComponentsInChildren<Collider>(includeInactive: true))
    {
        Require(collider.bounds.size == Vector3.zero, "inactive prefab collider bounds must reproduce the live empty-bounds failure");
    }
}

PlantableBerries.TryRegister(scene);

Require(Plugin.Log.Errors.Count == 0, "production registration must not log a startup failure");
Require(Diagnostics.Events.Contains("Farming/plantable_berries_registered"), "production registration must emit its success event");
Require(pieceTable.m_pieces.Contains(raspberry), "raspberry bush must reach the Cultivator");
Require(pieceTable.m_pieces.Contains(blueberry), "blueberry bush must reach the Cultivator");
Require(pieceTable.m_pieces.Contains(cloudberry), "cloudberry bush must reach the Cultivator");

Require(PlantableBerries.TryGetGridSpacing(raspberry, out float raspberrySpacing), "raspberry spacing must be registered");
Require(PlantableBerries.TryGetGridSpacing(blueberry, out float blueberrySpacing), "blueberry spacing must be registered");
Require(PlantableBerries.TryGetGridSpacing(cloudberry, out float cloudberrySpacing), "cloudberry spacing must be registered");
RequireNear(raspberrySpacing, 1.42f, "raspberry spacing must use its native viewblock sphere");
RequireNear(blueberrySpacing, 1.6f, "blueberry spacing must use its native viewblock sphere");
RequireNear(cloudberrySpacing, 0.73952f, "cloudberry spacing must include native child scale");

foreach (GameObject bush in new[] { raspberry, blueberry, cloudberry })
{
    Piece piece = bush.GetComponent<Piece>()!;
    Pickable pickable = bush.GetComponent<Pickable>()!;
    ZNetView netView = bush.GetComponent<ZNetView>()!;
    Require(piece.m_groundPiece && piece.m_groundOnly, "berry placement must remain ground-only");
    Require(!piece.m_cultivatedGroundOnly, "berry placement must not require cultivation");
    Require(piece.m_resources.Length == 1, "berry placement must use one matching resource");
    Require(piece.m_resources[0].m_amount == PlantableBerries.BerryCost, "berry placement must cost exactly five berries");
    Require(piece.m_canBeRemoved, "berry pieces must reach the native Hammer removal eligibility check");
    Require(piece.m_resources[0].m_recover, "native Hammer removal must refund the placement cost");
    RequireNear(pickable.m_respawnTimeMinutes, 300f, "registration must preserve the native growth and respawn duration");

    bool naturalCanRemove = piece.CanBeRemoved();
    PlantableBerryRemovalPatch.Postfix(piece, ref naturalCanRemove);
    Require(!naturalCanRemove, $"naturally spawned {bush.name} must not be Hammer-removable");

    bool placementState = PlantableBerries.IsNewOwnedBerryPlacement(piece);
    Require(placementState, $"{bush.name} must meet the conditions for a new owned berry placement");
    piece.SetCreator(12345L);
    PlantableBerries.StartPlacedBerryEmpty(piece, placementState);

    bool plantedCanRemove = piece.CanBeRemoved();
    PlantableBerryRemovalPatch.Postfix(piece, ref plantedCanRemove);
    Require(plantedCanRemove, $"player-planted {bush.name} must be Hammer-removable");

    Require(piece.GetCreator() == 12345L, $"{bush.name} must preserve native creator ownership");
    Require(pickable.Picked, $"newly planted {bush.name} must start empty");
    Require(pickable.PickedTime > 0L, $"newly planted {bush.name} must start its native picked-time cycle");
    Require(netView.Invocations.Count == 1, $"{bush.name} must request exactly one native state transition");
    Require(netView.Invocations[0].Target == ZNetView.Everybody, $"{bush.name} empty state must replicate to every peer");
    Require(netView.Invocations[0].Method == "RPC_SetPicked" && netView.Invocations[0].Value,
        $"{bush.name} must use Pickable's native picked-state RPC");

    UnityEngine.Random.State randomStateBeforeCadence = UnityEngine.Random.state;
    RequireNear(pickable.m_respawnTimeMinutes, 300f,
        $"newly planted {bush.name} must begin with its native prefab duration");
    BerryRespawnPatch.Prefix(pickable);
    float firstRespawnSeconds = pickable.m_respawnTimeMinutes * 60f;
    Require(
        firstRespawnSeconds >= PlantableBerries.BerryRespawnMinimumSeconds
            && firstRespawnSeconds <= PlantableBerries.BerryRespawnMaximumSeconds,
        $"newly planted {bush.name} must produce its first yield within the crop cadence");
    Require(UnityEngine.Random.state.Value == randomStateBeforeCadence.Value,
        "cadence selection must preserve Unity's shared random state");

    Vector3 persistedPosition = netView.GetZDO().GetPosition();
    float persistedRespawnSeconds = PlantableBerries.ResolveBerryRespawnSeconds(
        persistedPosition,
        pickable.PickedTime);
    RequireNear(persistedRespawnSeconds, firstRespawnSeconds,
        $"{bush.name} must resolve the same first-yield deadline after reconnect");

    netView.GetZDO().m_uid = new ZDOID(1L, 900000u);
    pickable.m_respawnTimeMinutes = 300f;
    BerryRespawnPatch.Prefix(pickable);
    RequireNear(pickable.m_respawnTimeMinutes * 60f, firstRespawnSeconds,
        $"{bush.name} must preserve its deadline when a world reload remaps its ZDO ID");
    Require(!pickable.ShouldRespawn(new DateTime(pickable.PickedTime).AddSeconds(3999)),
        $"{bush.name} must remain empty before the minimum berry cadence");
    Require(pickable.ShouldRespawn(new DateTime(pickable.PickedTime).AddSeconds(5001)),
        $"{bush.name} must be ready after the maximum berry cadence");
    Require(!pickable.ShouldRespawn(new DateTime(pickable.PickedTime).AddSeconds(firstRespawnSeconds - 1)),
        $"{bush.name} must remain empty before its selected native deadline");
    Require(pickable.ShouldRespawn(new DateTime(pickable.PickedTime).AddSeconds(firstRespawnSeconds + 1)),
        $"{bush.name} must become ready after its selected native deadline");

    ZNet.instance.Time = ZNet.instance.Time.AddSeconds(10);
    pickable.SetPicked(true);
    pickable.m_respawnTimeMinutes = 300f;
    BerryRespawnPatch.Prefix(pickable);
    float repeatedRespawnSeconds = pickable.m_respawnTimeMinutes * 60f;
    Require(
        repeatedRespawnSeconds >= PlantableBerries.BerryRespawnMinimumSeconds
            && repeatedRespawnSeconds <= PlantableBerries.BerryRespawnMaximumSeconds,
        $"harvested {bush.name} must regrow within the crop cadence");
    RequireNear(
        PlantableBerries.ResolveBerryRespawnSeconds(netView.GetZDO().GetPosition(), pickable.PickedTime),
        repeatedRespawnSeconds,
        $"{bush.name} regrowth must preserve its deadline across reconnect");
    Require(!pickable.ShouldRespawn(new DateTime(pickable.PickedTime).AddSeconds(repeatedRespawnSeconds - 1)),
        $"harvested {bush.name} must remain empty before its repeated native deadline");
    Require(pickable.ShouldRespawn(new DateTime(pickable.PickedTime).AddSeconds(repeatedRespawnSeconds + 1)),
        $"harvested {bush.name} must become ready after its repeated native deadline");

    bool repeatedState = PlantableBerries.IsNewOwnedBerryPlacement(piece);
    piece.SetCreator(12345L);
    PlantableBerries.StartPlacedBerryEmpty(piece, repeatedState);
    Require(!repeatedState && netView.Invocations.Count == 1,
        $"an already-created {bush.name} must never restart its growth cycle");
}

var remoteBush = CreateBush("RaspberryBush", CreateBerryItem("RemoteRaspberry"));
remoteBush.AddComponent<Piece>();
remoteBush.GetComponent<ZNetView>()!.Owner = false;
Require(!PlantableBerries.IsNewOwnedBerryPlacement(remoteBush.GetComponent<Piece>()!),
    "a non-owner must not initialize persistent picked state");

foreach (GameObject wildBush in new[]
{
    CreateBush("RaspberryBush", CreateBerryItem("WildRaspberry")),
    CreateBush("BlueberryBush", CreateBerryItem("WildBlueberry")),
    CreateBush("CloudberryBush", CreateBerryItem("WildCloudberry")),
})
{
    Pickable wildPickable = wildBush.GetComponent<Pickable>()!;
    ZNetView wildNetView = wildBush.GetComponent<ZNetView>()!;
    Require(!wildPickable.Picked, $"natural {wildBush.name} must keep its initial visible berries");
    RequireNear(wildPickable.m_respawnTimeMinutes, 300f,
        $"unpicked natural {wildBush.name} must retain the native prefab duration");

    wildPickable.SetPicked(true);
    BerryRespawnPatch.Prefix(wildPickable);
    float wildRespawnSeconds = wildPickable.m_respawnTimeMinutes * 60f;
    Require(
        wildRespawnSeconds >= PlantableBerries.BerryRespawnMinimumSeconds
            && wildRespawnSeconds <= PlantableBerries.BerryRespawnMaximumSeconds,
        $"harvested natural {wildBush.name} must use the berry cadence");
    Require(!wildPickable.ShouldRespawn(new DateTime(wildPickable.PickedTime).AddSeconds(wildRespawnSeconds - 1)),
        $"natural {wildBush.name} must remain empty before its selected deadline");
    Require(wildPickable.ShouldRespawn(new DateTime(wildPickable.PickedTime).AddSeconds(wildRespawnSeconds + 1)),
        $"natural {wildBush.name} must become ready after its selected deadline");

    wildNetView.GetZDO().m_uid = new ZDOID(1L, 800000u);
    wildPickable.m_respawnTimeMinutes = 300f;
    BerryRespawnPatch.Prefix(wildPickable);
    RequireNear(wildPickable.m_respawnTimeMinutes * 60f, wildRespawnSeconds,
        $"natural {wildBush.name} must preserve its deadline across world reload");

    ZNet.instance.Time = ZNet.instance.Time.AddSeconds(10);
    wildPickable.SetPicked(true);
    wildPickable.m_respawnTimeMinutes = 300f;
    BerryRespawnPatch.Prefix(wildPickable);
    float repeatedWildRespawnSeconds = wildPickable.m_respawnTimeMinutes * 60f;
    Require(
        repeatedWildRespawnSeconds >= PlantableBerries.BerryRespawnMinimumSeconds
            && repeatedWildRespawnSeconds <= PlantableBerries.BerryRespawnMaximumSeconds,
        $"natural {wildBush.name} must use the berry cadence after every harvest");
    Require(!wildPickable.ShouldRespawn(new DateTime(wildPickable.PickedTime).AddSeconds(repeatedWildRespawnSeconds - 1)),
        $"natural {wildBush.name} must remain empty before its repeated deadline");
    Require(wildPickable.ShouldRespawn(new DateTime(wildPickable.PickedTime).AddSeconds(repeatedWildRespawnSeconds + 1)),
        $"natural {wildBush.name} must become ready after its repeated deadline");
}

Piece removableRaspberry = raspberry.GetComponent<Piece>()!;
(bool wardRemoved, int wardRefund, _) = TryNativeHammerRemoval(
    removableRaspberry,
    removingPlayerId: 67890L,
    toolPieces: hammerPieceTable,
    wardAccess: false);
Require(!wardRemoved && wardRefund == 0 && removableRaspberry.DropResourcesCalls == 0,
    "native ward denial must block planted-bush removal and refund");

(bool cultivatorRemoved, int cultivatorRefund, _) = TryNativeHammerRemoval(
    removableRaspberry,
    removingPlayerId: 67890L,
    toolPieces: pieceTable,
    wardAccess: true);
Require(!cultivatorRemoved && cultivatorRefund == 0 && removableRaspberry.DropResourcesCalls == 0,
    "the Cultivator must not gain a removal path");

(bool peerRemoved, int peerRefund, string? peerRefundItem) = TryNativeHammerRemoval(
    removableRaspberry,
    removingPlayerId: 67890L,
    toolPieces: hammerPieceTable,
    wardAccess: true);
Require(peerRemoved, "a ward-permitted peer must remove a bush planted by another player");
Require(peerRefund == PlantableBerries.BerryCost && peerRefundItem == "Raspberry",
    "native Hammer removal must refund exactly five matching berries");
Require(removableRaspberry.DropResourcesCalls == 1,
    "successful native removal must drop resources exactly once");

(bool duplicateRemoved, int duplicateRefund, _) = TryNativeHammerRemoval(
    removableRaspberry,
    removingPlayerId: 67890L,
    toolPieces: hammerPieceTable,
    wardAccess: true);
Require(!duplicateRemoved && duplicateRefund == 0 && removableRaspberry.DropResourcesCalls == 1,
    "a duplicate removal attempt must not destroy or refund twice");

var unrelatedPickable = CreateBush("Pickable_Mushroom", CreateBerryItem("Mushroom"));
Piece unrelatedPiece = unrelatedPickable.AddComponent<Piece>();
unrelatedPiece.SetCreator(12345L);
Pickable unrelated = unrelatedPickable.GetComponent<Pickable>()!;
unrelated.SetPicked(true);
BerryRespawnPatch.Prefix(unrelated);
RequireNear(unrelated.m_respawnTimeMinutes, 300f,
    "an unrelated Pickable must retain its native respawn");
bool unrelatedNativeDenial = false;
PlantableBerryRemovalPatch.Postfix(unrelatedPiece, ref unrelatedNativeDenial);
Require(!unrelatedNativeDenial, "an unrelated Piece must preserve a native removal denial");
bool unrelatedNativeApproval = true;
PlantableBerryRemovalPatch.Postfix(unrelatedPiece, ref unrelatedNativeApproval);
Require(unrelatedNativeApproval, "an unrelated Piece must preserve a native removal approval");

Console.WriteLine("Plantable berry production registration tests passed");
