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
    bush.AddComponent<Pickable>().m_itemPrefab = berryItem;
    return bush;
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

var pieceTable = new PieceTable();
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
    Require(piece.m_groundPiece && piece.m_groundOnly, "berry placement must remain ground-only");
    Require(!piece.m_cultivatedGroundOnly, "berry placement must not require cultivation");
    Require(piece.m_resources.Length == 1, "berry placement must use one matching resource");
    Require(piece.m_resources[0].m_amount == PlantableBerries.BerryCost, "berry placement must cost exactly five berries");
    Require(!piece.m_resources[0].m_recover, "berry placement cost must remain nonrecoverable");
}

Console.WriteLine("Plantable berry production registration tests passed");
