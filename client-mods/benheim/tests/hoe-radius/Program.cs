using System;
using System.Linq;
using System.Text.Json;
using BenheimQoL.Farming;
using BenheimQoL.Infrastructure;
using UnityEngine;

Diagnostics.Events.Clear();
Player player = PlayerWithTool("Hoe", "paved_road");
Player.m_localPlayer = player;
InputState.LeftShiftHeld = true;
TerrainOp operation = TerrainAction("paved_road(Clone)", 2f, 1f, 3f, 4f);

Require(HoeRadiusExpansion.TryPrepareOperation(operation), "held Left Shift prepares the selected Hoe terrain operation");
Require(Radii(operation.m_settings, 6f, 3f, 9f, 12f), "every enabled native terrain radius is multiplied by exactly three");
Require(Last().GetProperty("event").GetString() == "hoe_radius_action_requested" &&
    Last().GetProperty("radius_multiplier").GetSingle() == 3f,
    "the request records the exact multiplier through typed diagnostics");

ZPackage firstPacket = new();
ZPackage secondPacket = new();
Require(HoeRadiusExpansion.TryWriteProtocol(operation.m_settings, firstPacket) &&
    HoeRadiusExpansion.TryWriteProtocol(operation.m_settings, secondPacket),
    "every heightmap RPC for one native action receives the same expansion marker");
firstPacket.SetPos(0);
TerrainOp.Settings canonical = TerrainAction("paved_road", 2f, 1f, 3f, 4f).m_settings;
TerrainOp.Settings? received = canonical;
Require(HoeRadiusExpansion.TryReadProtocol(firstPacket, ref received), "a compatible terrain owner accepts the radius extension");
Require(received != null && !ReferenceEquals(canonical, received) && Radii(canonical, 2f, 1f, 3f, 4f),
    "receiving an expanded action never mutates ObjectDB's shared native settings");
Require(Radii(received!, 6f, 3f, 9f, 12f), "the terrain owner reconstructs the same three-times radii");
HoeRadiusExpansion.RecordApplied(received!);
Require(Last().GetProperty("event").GetString() == "hoe_radius_action_finished" &&
    Last().GetProperty("result").GetString() == "applied",
    "return from the native terrain operation records the actual applied outcome");

ZPackage nativePacket = new();
TerrainOp.Settings? nativeReceived = canonical;
Require(!HoeRadiusExpansion.TryReadProtocol(nativePacket, ref nativeReceived) && ReferenceEquals(canonical, nativeReceived),
    "an ordinary or older-sender terrain packet remains native");
ZPackage unknownPacket = new();
unknownPacket.Write(123);
unknownPacket.Write(456);
unknownPacket.Write("not-benheim");
unknownPacket.SetPos(0);
TerrainOp.Settings? unknownReceived = canonical;
Require(!HoeRadiusExpansion.TryReadProtocol(unknownPacket, ref unknownReceived) && unknownPacket.GetPos() == 0,
    "an unknown packet suffix is rejected without consuming another mod's data");

InputState.LeftShiftHeld = false;
TerrainOp released = TerrainAction("paved_road(Clone)", 2f, 1f, 3f, 4f);
Require(!HoeRadiusExpansion.TryPrepareOperation(released) && Radii(released.m_settings, 2f, 1f, 3f, 4f),
    "releasing Left Shift restores the native action immediately");
InputState.LeftShiftHeld = true;
player.RightItem!.m_dropPrefab = new GameObject("Cultivator");
TerrainOp cultivator = TerrainAction("cultivate(Clone)", 2f, 1f, 3f, 4f);
Require(!HoeRadiusExpansion.TryPrepareOperation(cultivator) && Radii(cultivator.m_settings, 2f, 1f, 3f, 4f),
    "the Cultivator never qualifies for Hoe radius expansion");
player.RightItem.m_dropPrefab = new GameObject("Hoe");
TerrainOp.m_forceDisableTerrainOps = true;
TerrainOp ghostOperation = TerrainAction("paved_road(Clone)", 2f, 1f, 3f, 4f);
Require(!HoeRadiusExpansion.TryPrepareOperation(ghostOperation), "native placement-ghost construction cannot execute or mark a terrain action");
TerrainOp.m_forceDisableTerrainOps = false;

GameObject ghost = new("paved_road");
Transform ghostOnly = ghost.transform.Add("_GhostOnly", new Vector3(2f, 1f, 4f));
Transform nonTerrainFeedback = ghostOnly.Add("NonTerrainFeedback", new Vector3(0.5f, 2f, 0.25f));
ParticleSystem ghostEffect = new(new Vector3(0.25f, 0.5f, 0.75f));
ghostOnly.AddComponentInChildren(ghostEffect);
Transform ghostVisual = ghostEffect.transform;
player.m_placementGhost = ghost;
InputState.LeftShiftHeld = true;
HoeRadiusPreview.Update(player);
Require(Scale(ghostVisual.localScale, 3f, 3f, 3f),
    "the held preview uses the absolute three-times radius ratio on the nested terrain particle effect");
Require(Scale(ghostOnly.localScale, 2f, 1f, 4f),
    "preview expansion does not scale the authored _GhostOnly transform");
Require(Scale(nonTerrainFeedback.lossyScale, 1f, 2f, 1f),
    "preview expansion cannot broaden unrelated grass, foliage, or interaction feedback under _GhostOnly");
int previewEvents = Diagnostics.Events.Count;
HoeRadiusPreview.Update(player);
Require(Diagnostics.Events.Count == previewEvents, "an unchanged held preview does not emit frame-loop spam");
InputState.LeftShiftHeld = false;
HoeRadiusPreview.Update(player);
Require(Scale(ghostVisual.localScale, 0.25f, 0.5f, 0.75f),
    "releasing Left Shift restores the particle effect's loaded native scale immediately");
Require(Scale(ghostOnly.localScale, 2f, 1f, 4f) && Scale(nonTerrainFeedback.lossyScale, 1f, 2f, 1f),
    "releasing Left Shift leaves all non-terrain ghost feedback at native radius");
player.RightItem.m_dropPrefab = new GameObject("Cultivator");
InputState.LeftShiftHeld = true;
HoeRadiusPreview.Update(player);
Require(Scale(ghostVisual.localScale, 0.25f, 0.5f, 0.75f),
    "the Cultivator preview remains native even while Left Shift is held");
player.RightItem.m_dropPrefab = new GameObject("Hoe");
player.m_placementGhost = new GameObject("paved_road_missing_visual");
HoeRadiusPreview.Update(player);
Require(Last().GetProperty("event").GetString() == "hoe_radius_preview" &&
    Last().GetProperty("result").GetString() == "failed" &&
    Last().GetProperty("reason").GetString() == "ghost_visual_missing",
    "a changed native ghost hierarchy fails visibly through structured evidence");
HoeRadiusPreview.Reset();

Console.WriteLine("Hoe radius action, protocol, isolation, preview, and evidence checks passed");

static Player PlayerWithTool(string tool, string action)
{
    TerrainOp selectedOperation = TerrainAction(action, 2f, 1f, 3f, 4f);
    Piece selected = new() { gameObject = selectedOperation.gameObject };
    return new Player
    {
        PlaceMode = true,
        RightItem = new ItemDrop.ItemData { m_dropPrefab = new GameObject(tool) },
        SelectedPiece = selected,
    };
}

static TerrainOp TerrainAction(string name, float level, float raise, float smooth, float paint)
{
    TerrainOp operation = new()
    {
        gameObject = new GameObject(name),
        m_settings = new TerrainOp.Settings
        {
            m_level = true,
            m_levelRadius = level,
            m_raise = true,
            m_raiseRadius = raise,
            m_smooth = true,
            m_smoothRadius = smooth,
            m_paintCleared = true,
            m_paintRadius = paint,
        },
    };
    operation.gameObject.AddComponent(operation);
    return operation;
}

static bool Radii(TerrainOp.Settings value, float level, float raise, float smooth, float paint) =>
    value.m_levelRadius == level && value.m_raiseRadius == raise &&
    value.m_smoothRadius == smooth && value.m_paintRadius == paint;
static bool Scale(Vector3 value, float x, float y, float z) => value.x == x && value.y == y && value.z == z;
static JsonElement Last() => JsonDocument.Parse(Diagnostics.Events.Last().ToJsonLine()).RootElement;
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
