using System;
using System.Collections.Generic;
using BenheimQoL.Grappling;
using BenheimQoL.Infrastructure;
using UnityEngine;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Near(float expected, float actual, string message)
{
    if (Math.Abs(expected - actual) > 0.001f)
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
}

static GameObject PointPrefab()
{
    var prefab = new GameObject("rope");
    prefab.AddComponent(new GrapplingPoint());
    return prefab;
}

static ItemDrop.ItemData Hook(bool requiresReload = true)
{
    var item = new ItemDrop.ItemData();
    item.m_shared.m_attack.m_spawnOnHit = PointPrefab();
    item.m_shared.m_attack.m_requiresReload = requiresReload;
    return item;
}

static GrapplingPoint Point(float x, float y, float z, float nativeLength = 50f)
{
    return new GrapplingPoint
    {
        m_maxLength = nativeLength,
        transform = { position = new Vector3(x, y, z) }
    };
}

static Projectile Fire(Player player, ItemDrop.ItemData hook)
{
    Player.m_localPlayer = player;
    var projectile = new Projectile { m_ttl = 4f };
    GrapplingRuntime.Fired(projectile, player, hook);
    return projectile;
}

static void Attach(Projectile projectile, GrapplingPoint point, Player player)
{
    GrapplingRuntime.Traversal? previous = GrapplingRuntime.EnterHit(projectile);
    Require(previous == null, "a fresh attachment starts outside a spawn scope");
    GrapplingRuntime.Activate(point, player);
    GrapplingRuntime.RestoreScope(previous);
}

static void Release(GrapplingPoint point, Player player, bool early = false)
{
    var owner = new ZNetView();
    GrapplingRuntime.BreakScope scope = GrapplingRuntime.EnterBreak(point, owner, player);
    owner.Owner = false; // Native ZNetScene.Destroy resets ownership before Harmony postfix.
    GrapplingRuntime.Released(point, early, scope.Accepted);
    GrapplingRuntime.RestoreScope(scope.Previous);
}

static int EventCount(string name) => Diagnostics.Events.FindAll(value => value.Name == name).Count;

// Score gates are directly linked from GrapplingRules.cs.
Near(0f, GrapplingRules.Experience(2.99f, 10f, 10f, 10f), "nearby attachments reject XP");
Near(0f, GrapplingRules.Experience(10f, 2.99f, 10f, 10f), "motionless tether rejects XP");
Near(0f, GrapplingRules.Experience(10f, 10f, 2.99f, 10f), "small net displacement rejects XP");
Near(0f, GrapplingRules.Experience(float.NaN, 10f, 10f, 0f), "non-finite distance rejects XP");
Near(0.65f, GrapplingRules.Experience(10f, 10f, 10f, 0f), "base award plus displacement");
Near(0.75f, GrapplingRules.Experience(10f, 10f, 10f, 5f), "uphill adds XP");
Near(0.65f, GrapplingRules.Experience(10f, 10f, 10f, -5f), "downhill gets no uphill bonus");
Near(GrapplingRules.MaximumExperience,
    GrapplingRules.Experience(1000f, 1000f, 1000f, 1000f), "per-grapple award is capped");

// Multipliers are clamped and improve both reach and reload at the level endpoints.
Near(1f, GrapplingRules.ReachMultiplier(0f), "level-zero reach");
Near(1.5f, GrapplingRules.ReachMultiplier(50f), "mid-level reach");
Near(2f, GrapplingRules.ReachMultiplier(100f), "max-level reach");
Near(2f, GrapplingRules.ReachMultiplier(150f), "reach clamps above max level");
Near(1f, GrapplingRules.ReloadMultiplier(0f), "level-zero reload");
Near(0.75f, GrapplingRules.ReloadMultiplier(50f), "mid-level reload");
Near(0.5f, GrapplingRules.ReloadMultiplier(100f), "max-level reload");
Near(1f, GrapplingRules.ReachMultiplier(float.NaN), "invalid level fails to base reach");

var definitionSkills = new Skills();
GrapplingSkill.Register(definitionSkills);
int customSkillId = (int)GrapplingSkill.Type;
Require(customSkillId > (int)Skills.SkillType.All, "custom skill ID is outside native enum values");
Require(definitionSkills.m_skills.Count == 1, "registration adds one skill definition");
GrapplingSkill.Register(definitionSkills);
Require(definitionSkills.m_skills.Count == 1, "registration is idempotent");
Require(GrapplingSkill.LocalizationKey == "skill_" + customSkillId, "native skill token follows its persisted ID");

// Hook flight expiration and nearby/motionless attachment all exercise the linked production runtime.
var hook = Hook();
var missPlayer = new Player { SkillLevel = 50f };
var miss = Fire(missPlayer, hook);
Near(6f, miss.m_ttl, "flight TTL is extended at level 50");
miss.m_ttl = 0f;
GrapplingRuntime.FlightUpdated(miss, didHit: false);
Require(missPlayer.RaisedExperience.Count == 0, "expired miss raises no skill");
Require(Diagnostics.Events.Exists(value => value.Name == "traversal_finished"
    && Equals(value.Fields["reason"], "miss") && Equals(value.Fields["experience"], 0f)),
    "miss closes the traversal with zero experience");

var unattachedPlayer = new Player();
var hitWithoutRope = Fire(unattachedPlayer, hook);
GrapplingRuntime.FlightUpdated(hitWithoutRope, didHit: true);
Require(unattachedPlayer.RaisedExperience.Count == 0
    && Diagnostics.Events.Exists(value => value.Name == "traversal_finished"
        && Equals(value.Fields["reason"], "no_attachment") && Equals(value.Fields["experience"], 0f)),
    "a hit that creates no local rope earns no XP");

var unlimitedPlayer = new Player();
Player.m_localPlayer = unlimitedPlayer;
var unlimitedFlight = new Projectile { m_ttl = 0f };
GrapplingRuntime.Fired(unlimitedFlight, unlimitedPlayer, hook);
int finishedBeforeUnlimitedFlights = EventCount("traversal_finished");
GrapplingRuntime.FlightUpdated(unlimitedFlight, didHit: false);
var negativeTtlPlayer = new Player();
Player.m_localPlayer = negativeTtlPlayer;
var negativeTtlFlight = new Projectile { m_ttl = -1f };
GrapplingRuntime.Fired(negativeTtlFlight, negativeTtlPlayer, hook);
GrapplingRuntime.FlightUpdated(negativeTtlFlight, didHit: false);
Require(EventCount("traversal_finished") == finishedBeforeUnlimitedFlights,
    "nonpositive native TTLs do not fabricate an immediate miss");
var unlimitedScope = GrapplingRuntime.EnterHit(unlimitedFlight);
var unlimitedPoint = Point(10f, 0f, 0f);
GrapplingRuntime.Activate(unlimitedPoint, unlimitedPlayer);
GrapplingRuntime.RestoreScope(unlimitedScope);
Require(unlimitedPoint.m_maxLength == 50f, "native unlimited flight TTL remains eligible for later attachment");

var localForRemoteShot = new Player();
Player.m_localPlayer = localForRemoteShot;
var remoteOwner = new Player();
var remoteShot = new Projectile { m_ttl = 4f };
int firedBeforeRemoteShot = EventCount("shot_fired");
GrapplingRuntime.Fired(remoteShot, remoteOwner, hook);
Near(4f, remoteShot.m_ttl, "a remote player's projectile keeps its native TTL");
Require(EventCount("shot_fired") == firedBeforeRemoteShot,
    "a remote player's projectile is not claimed by the local traversal tracker");

var nearbyPlayer = new Player();
var nearbyProjectile = Fire(nearbyPlayer, hook);
var nearbyPoint = Point(1f, 0f, 0f);
Attach(nearbyProjectile, nearbyPoint, nearbyPlayer);
nearbyPlayer.transform.position = new Vector3(4f, 0f, 0f);
Release(nearbyPoint, nearbyPlayer);
Require(nearbyPlayer.RaisedExperience.Count == 0, "nearby hook hit earns no XP even after running away");

var stillPlayer = new Player();
var stillProjectile = Fire(stillPlayer, hook);
stillPlayer.transform.position = new Vector3(10f, 0f, 0f); // movement happened before the rope attached
var distantPoint = Point(20f, 0f, 0f);
Attach(stillProjectile, distantPoint, stillPlayer);
Release(distantPoint, stillPlayer);
Require(stillPlayer.RaisedExperience.Count == 0,
    "movement during projectile flight does not reward hanging motionless afterward");

var ownershipPlayer = new Player();
var ownershipProjectile = Fire(ownershipPlayer, hook);
var ownershipPoint = Point(10f, 0f, 0f);
Attach(ownershipProjectile, ownershipPoint, ownershipPlayer);
ownershipPlayer.transform.position = new Vector3(5f, 0f, 0f);
var nonOwnerView = new ZNetView { Owner = false };
GrapplingRuntime.BreakScope rejectedScope = GrapplingRuntime.EnterBreak(ownershipPoint, nonOwnerView, ownershipPlayer);
Require(!rejectedScope.Accepted, "a non-owner cannot claim a traversal release");
GrapplingRuntime.Released(ownershipPoint, early: false, accepted: rejectedScope.Accepted);
GrapplingRuntime.RestoreScope(rejectedScope.Previous);
Require(ownershipPlayer.RaisedExperience.Count == 0, "non-owner release awards no XP");
Release(ownershipPoint, ownershipPlayer);
Require(ownershipPlayer.RaisedExperience.Count == 1,
    "entry ownership snapshot survives native view reset before the postfix");

// Native Break(false) activates its hanging rope before the old Break(true) reenters.
// Reproduce that order through the production scope/activation/release methods.
var traversalPlayer = new Player { SkillLevel = 50f, Health = 1f };
var traversalProjectile = Fire(traversalPlayer, hook);
var pullPoint = Point(10f, 2f, 0f, nativeLength: 20f);
var priorScope = GrapplingRuntime.EnterHit(traversalProjectile);
GrapplingRuntime.Activate(pullPoint, traversalPlayer);
GrapplingRuntime.RestoreScope(priorScope);
Near(30f, pullPoint.m_maxLength, "pull rope break distance follows reach progression");
var hangPoint = Point(11f, 2f, 0f, nativeLength: 8f);
var outerView = new ZNetView();
GrapplingRuntime.BreakScope outerScope = GrapplingRuntime.EnterBreak(pullPoint, outerView, traversalPlayer);
GrapplingRuntime.Activate(hangPoint, traversalPlayer);
var recursiveView = new ZNetView();
GrapplingRuntime.BreakScope recursiveScope = GrapplingRuntime.EnterBreak(pullPoint, recursiveView, traversalPlayer);
GrapplingRuntime.Released(pullPoint, early: true, recursiveScope.Accepted);
GrapplingRuntime.RestoreScope(recursiveScope.Previous);
outerView.Owner = false; // Outer Break completes after the recursive break has already removed its rope.
GrapplingRuntime.Released(pullPoint, early: false, outerScope.Accepted);
GrapplingRuntime.RestoreScope(outerScope.Previous);
Near(12f, hangPoint.m_maxLength, "hanging rope inherits the same reach progression");
DiagnosticEvent[] attachEvents = Diagnostics.Events.FindAll(value => value.Name == "rope_attached").ToArray();
string pullOperation = (string)attachEvents[attachEvents.Length - 2].Fields["operation_id"]!;
string hangOperation = (string)attachEvents[attachEvents.Length - 1].Fields["operation_id"]!;
Require(pullOperation == hangOperation, "pull-to-hang carries one traversal operation across ropes");
traversalPlayer.transform.position = new Vector3(11f, 6f, 0f);
int eventCountBeforeRelease = EventCount("traversal_finished");
Release(hangPoint, traversalPlayer);
Require(traversalPlayer.RaisedExperience.Count == 1, "handoff awards experience once on final rope release");
Near(0.25f + Vector3.Distance(Vector3.zero, traversalPlayer.transform.position) * 0.04f + 6f * 0.02f,
    traversalPlayer.RaisedExperience[0], "low health does not change traversal score");
Release(hangPoint, traversalPlayer);
Require(traversalPlayer.RaisedExperience.Count == 1, "duplicate release cannot award twice");
Require(EventCount("traversal_finished") == eventCountBeforeRelease + 1,
    "only the terminal hanging rope emits a traversal finish");

var remotePlayer = new Player { SkillLevel = 100f };
var levelledHook = Hook();
var flight = Fire(remotePlayer, levelledHook);
Near(8f, flight.m_ttl, "level 100 doubles fired hook time");
var rope = Point(10f, 0f, 0f, nativeLength: 25f);
priorScope = GrapplingRuntime.EnterHit(flight);
GrapplingRuntime.Activate(rope, remotePlayer);
GrapplingRuntime.RestoreScope(priorScope);
Near(50f, rope.m_maxLength, "level 100 doubles usable rope break distance");
Near(10f, GrapplingRuntime.Reload(levelledHook, 20f), "level 100 halves hook reload");
Near(20f, GrapplingRuntime.Reload(Hook(requiresReload: false), 20f), "no-reload hook keeps native duration");
var ordinaryItem = new ItemDrop.ItemData();
Near(20f, GrapplingRuntime.Reload(ordinaryItem, 20f), "non-hook item keeps native duration");
Player.m_localPlayer = null!;
Near(20f, GrapplingRuntime.Reload(levelledHook, 20f), "no local player keeps native duration");

Player.m_localPlayer = remotePlayer;
var queued = new List<Player.MinorActionData>
{
    new Player.MinorActionData { m_type = Player.MinorActionData.ActionType.Reload, m_item = levelledHook }
};
int reloadEvents = EventCount("reload_queued");
GrapplingRuntime.ObserveReload(remotePlayer, 1, queued); // Existing queue entries are not new actions.
GrapplingRuntime.ObserveReload(remotePlayer, 0, queued);
GrapplingRuntime.ObserveReload(new Player(), 0, queued);
Require(EventCount("reload_queued") == reloadEvents + 1,
    "reload evidence is limited to a new local grappling-hook reload action");

Console.WriteLine("Grappling production-rule and runtime fixtures passed (gameplay runtime remains unproven)");
