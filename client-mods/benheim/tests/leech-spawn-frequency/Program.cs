using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BenheimQoL.DeveloperDiagnostics;
using BenheimQoL.Infrastructure;
using BenheimQoL.Spawning;
using HarmonyLib;
using UnityEngine;

var nativeInterval = 200f;
ExpectClose(200f / 5f, LeechSpawnFrequency.AdjustInterval(nativeInterval), "native interval is divided by five");
ExpectClose(0f, LeechSpawnFrequency.AdjustInterval(0f), "zero native interval remains zero");

var state = new LeechSpawnAdjustmentState<SpawnData>();
var shared = new SpawnData();
ExpectTrue(!state.Contains(shared), "unclaimed SpawnData is not adjusted");
ExpectTrue(state.TryClaim(shared), "first shared SpawnData adjustment claims the reference");
ExpectTrue(state.Contains(shared), "claimed SpawnData is identifiable at the successful spawn seam");
ExpectTrue(!state.TryClaim(shared), "repeated shared SpawnData initialization is idempotent");
ExpectTrue(state.TryClaim(new SpawnData()), "a distinct SpawnData reference is independently claimable");

Type spawnPatch = typeof(LeechSpawnPatches).GetNestedType("SuccessfulSpawnPatch", BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("successful spawn Harmony patch is missing");
CustomAttributeData patchTarget = spawnPatch.CustomAttributes.Single(attribute =>
    attribute.AttributeType == typeof(HarmonyPatch));
ExpectTrue((Type?)patchTarget.ConstructorArguments[0].Value == typeof(SpawnSystem), "success patch targets SpawnSystem");
ExpectTrue((string?)patchTarget.ConstructorArguments[1].Value == "Spawn", "success patch targets the native Spawn method");
var patchedParameters = (IList<CustomAttributeTypedArgument>)patchTarget.ConstructorArguments[2].Value!;
Type[] expectedParameters =
{
    typeof(SpawnSystem.SpawnData),
    typeof(Vector3),
    typeof(bool),
    typeof(int),
    typeof(float)
};
ExpectTrue(
    patchedParameters.Select(argument => (Type)argument.Value!).SequenceEqual(expectedParameters),
    "success patch targets the exact five-argument native Spawn overload");

MethodInfo prefix = spawnPatch.GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException("successful spawn prefix is missing");
MethodInfo postfix = spawnPatch.GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException("successful spawn postfix is missing");
ExpectTrue(prefix.CustomAttributes.Any(attribute => attribute.AttributeType == typeof(HarmonyPrefix)), "prefix keeps its HarmonyPrefix annotation");
ExpectTrue(postfix.CustomAttributes.Any(attribute => attribute.AttributeType == typeof(HarmonyPostfix)), "postfix keeps its HarmonyPostfix annotation");
ExpectParameters(
    prefix,
    new[] { "critter", "eventSpawner", "__state" },
    new[] { typeof(SpawnSystem.SpawnData), typeof(bool), typeof(bool).MakeByRefType() },
    "prefix Harmony argument binding");
ExpectTrue(prefix.GetParameters()[2].IsOut, "prefix writes Harmony state through an out parameter");
ExpectParameters(
    postfix,
    new[] { "critter", "__state" },
    new[] { typeof(SpawnSystem.SpawnData), typeof(bool) },
    "postfix Harmony argument binding");

var leechPrefab = new GameObject(LeechSpawnFrequency.PrefabName);
ZNetScene.instance = new ZNetScene(leechPrefab);
var adjustedLeech = new SpawnSystem.SpawnData
{
    m_prefab = leechPrefab,
    m_spawnInterval = nativeInterval,
    m_spawnChance = 50f,
    m_maxSpawned = 10,
    m_groupSizeMin = 1,
    m_groupSizeMax = 1,
    m_spawnDistance = 5f,
    m_biome = Heightmap.Biome.Swamp,
    m_biomeArea = Heightmap.BiomeArea.Everything,
    m_minAltitude = -1f,
    m_maxAltitude = 1000f,
};
var spawnSystem = new SpawnSystem(adjustedLeech);
UnityEngine.Time.realtimeSinceStartup = 0f;
SpawnSystem.LoadedInstances = 0;
ExpectTrue(
    SpawnPopulationProbe.TrySetActive(true, out _),
    "shipped-on spawn probe activates without changing gameplay");
InvokePostfix(spawnSystem, "InvokeAwake");
ExpectClose(40f, adjustedLeech.m_spawnInterval, "registered rule exposes its adjusted effective interval");
ExpectTrue(Diagnostics.Emitted.Count == 2, "rule availability emits one configuration and one population event");
DiagnosticEvent configuration = Diagnostics.Emitted[0];
ExpectTrue(configuration.Domain == "Spawning", "configuration event uses the Spawning domain");
ExpectTrue(configuration.Name == "spawn_probe_configuration", "configuration event identifies the spawn probe");
ExpectTrue((string)configuration.Fields["source"] == "base_world", "configuration identifies the registered source");
ExpectTrue((string)configuration.Fields["prefab"] == LeechSpawnFrequency.PrefabName, "configuration identifies the registered prefab");
ExpectClose(40f, (float)configuration.Fields["spawn_interval_seconds"], "configuration records the effective interval");
ExpectClose(50f, (float)configuration.Fields["configured_spawn_chance_percent"], "configuration records configured chance");
ExpectTrue((int)configuration.Fields["configured_loaded_population_cap"] == 10, "configuration records the configured loaded cap");
ExpectTrue((int)configuration.Fields["group_size_min"] == 1, "configuration records group minimum");
ExpectTrue((int)configuration.Fields["group_size_max"] == 1, "configuration records group maximum");
ExpectClose(5f, (float)configuration.Fields["same_prefab_spacing_meters"], "configuration records same-prefab spacing");
ExpectTrue((string)configuration.Fields["biome"] == "Swamp", "configuration records biome constraints");
ExpectClose(-1f, (float)configuration.Fields["min_altitude"], "configuration records minimum altitude");
ExpectClose(1000f, (float)configuration.Fields["max_altitude"], "configuration records maximum altitude");
ExpectTrue((int)configuration.Fields["loaded_count"] == 0, "configuration records loaded population");
ExpectTrue(!(bool)configuration.Fields["configured_cap_saturated"], "configuration records configured-cap saturation");
DiagnosticEvent initialPopulation = Diagnostics.Emitted[1];
ExpectTrue(initialPopulation.Name == "spawn_probe_population", "initial population uses the population event");
ExpectTrue((string)initialPopulation.Fields["reason"] == "available", "initial population identifies availability");

Diagnostics.Reset();
SpawnPopulationProbe.RegisterRule(
    "base_world",
    LeechSpawnFrequency.PrefabName,
    new SpawnSystem.SpawnData
    {
        m_prefab = leechPrefab,
        m_spawnInterval = 40f,
    });
ExpectTrue(Diagnostics.Emitted.Count == 0, "equivalent loaded rule instances do not multiply one probe stream");

SpawnSystem.LoadedInstances = 1;
UnityEngine.Time.realtimeSinceStartup = 4.9f;
SpawnPopulationProbe.Update();
ExpectTrue(Diagnostics.Emitted.Count == 0, "population work is rate-limited between samples");
UnityEngine.Time.realtimeSinceStartup = 5f;
SpawnPopulationProbe.Update();
ExpectPopulation("count_changed", loadedCount: 1, saturated: false);

Diagnostics.Reset();
SpawnSystem.LoadedInstances = 10;
UnityEngine.Time.realtimeSinceStartup = 10f;
SpawnPopulationProbe.Update();
ExpectPopulation("configured_cap_entered", loadedCount: 10, saturated: true);

Diagnostics.Reset();
SpawnSystem.LoadedInstances = 9;
UnityEngine.Time.realtimeSinceStartup = 15f;
SpawnPopulationProbe.Update();
ExpectPopulation("configured_cap_exited", loadedCount: 9, saturated: false);

Diagnostics.Reset();
UnityEngine.Time.realtimeSinceStartup = 20f;
SpawnPopulationProbe.Update();
ExpectTrue(Diagnostics.Emitted.Count == 0, "unchanged population emits nothing before the heartbeat");
UnityEngine.Time.realtimeSinceStartup = 75f;
SpawnPopulationProbe.Update();
ExpectPopulation("heartbeat", loadedCount: 9, saturated: false);

Diagnostics.Reset();
adjustedLeech.m_spawnChance = 60f;
UnityEngine.Time.realtimeSinceStartup = 80f;
SpawnPopulationProbe.Update();
ExpectTrue(Diagnostics.Emitted.Count == 1, "configuration change emits one bounded configuration event");
ExpectTrue(Diagnostics.Emitted[0].Name == "spawn_probe_configuration", "configuration change keeps the configuration schema");
ExpectTrue((string)Diagnostics.Emitted[0].Fields["reason"] == "changed", "configuration change identifies its reason");
ExpectClose(60f, (float)Diagnostics.Emitted[0].Fields["configured_spawn_chance_percent"], "configuration change records the new configured chance");

Diagnostics.Reset();
ExpectTrue(SpawnPopulationProbe.TrySetActive(false, out _), "probe can be disabled for the session");
SpawnSystem.LoadedInstances = 8;
UnityEngine.Time.realtimeSinceStartup = 85f;
SpawnPopulationProbe.Update();
ExpectTrue(Diagnostics.Emitted.Count == 0, "disabled event probe performs no sampling or emission");
ExpectTrue(SpawnPopulationProbe.TrySetActive(true, out _), "probe can be re-enabled");
ExpectTrue(Diagnostics.Emitted.Count == 2, "re-enable reports current configuration and population once");

Diagnostics.Reset();
SpawnSystem.ThrowOnCount = true;
UnityEngine.Time.realtimeSinceStartup = 90f;
SpawnPopulationProbe.Update();
SpawnSystem.ThrowOnCount = false;
ExpectTrue(DeveloperDiagnosticsRuntime.Failures.Count == 1, "native count failure is contained and reported once");
ExpectTrue(Diagnostics.Emitted.Count == 0, "failed sample emits no partial evidence");
UnityEngine.Time.realtimeSinceStartup = 95f;
SpawnPopulationProbe.Update();
ExpectTrue(Diagnostics.Emitted.Count == 0, "faulted rule does not retry every sampling interval");
ExpectTrue(SpawnPopulationProbe.TrySetActive(false, out _), "disable clears failed observation state");
ExpectTrue(SpawnPopulationProbe.TrySetActive(true, out _), "re-enable safely retries the registered rule");
ExpectTrue(Diagnostics.Emitted.Count == 2, "retry emits a complete current snapshot");

Diagnostics.Reset();
SpawnPopulationProbe.Cleanup(DiagnosticProbeCleanupReason.WorldExit);
InvokePostfix(spawnSystem, "InvokeAwake");
ExpectTrue(SpawnPopulationProbe.TrySetActive(true, out _), "new world reactivates the shipped-on probe");
ExpectTrue(Diagnostics.Emitted.Count == 2, "world re-entry repopulates the probe through the real consumer seam");
ExpectClose(
    40f,
    (float)Diagnostics.Emitted[0].Fields["spawn_interval_seconds"],
    "an already-adjusted reused rule registers without applying the multiplier twice");
ExpectTrue(
    (int)Diagnostics.Emitted[0].Fields["configured_loaded_population_cap"] == 10,
    "world re-entry reports the reused rule's configured cap");

Diagnostics.Reset();
SpawnSystem.m_nospawn = false;
InvokePatchedSpawn(spawnSystem, adjustedLeech, eventSpawner: false, prefix, postfix);
ExpectTrue(spawnSystem.SuccessfulSpawns == 1, "ordinary adjusted Leech reaches native spawn success");
ExpectTrue(Diagnostics.Emitted.Count == 1, "ordinary adjusted Leech emits exactly one typed success event");
DiagnosticEvent success = Diagnostics.Emitted[0];
ExpectTrue(success.Domain == "Spawning", "success event uses the Spawning domain");
ExpectTrue(success.Name == "leech_spawn_succeeded", "success event identifies completed Leech spawn");
ExpectTrue((string)success.Fields["source"] == "base_world", "success event identifies the base-world source");
ExpectTrue((string)success.Fields["prefab"] == LeechSpawnFrequency.PrefabName, "success event identifies the Leech prefab");
ExpectClose(5f, (float)success.Fields["opportunity_multiplier"], "success event records the adjusted opportunity multiplier");

SpawnSystem.m_nospawn = true;
InvokePatchedSpawn(spawnSystem, adjustedLeech, eventSpawner: false, prefix, postfix);
ExpectTrue(spawnSystem.SuccessfulSpawns == 1, "native no-spawn gate prevents instantiation");
ExpectTrue(Diagnostics.Emitted.Count == 1, "native no-spawn gate emits no success event");

SpawnSystem.m_nospawn = false;
InvokePatchedSpawn(spawnSystem, adjustedLeech, eventSpawner: true, prefix, postfix);
ExpectTrue(spawnSystem.SuccessfulSpawns == 2, "event Leech can complete native spawning");
ExpectTrue(Diagnostics.Emitted.Count == 1, "event Leech emits no adjusted base-world success event");

var unadjustedLeech = new SpawnSystem.SpawnData
{
    m_prefab = leechPrefab,
    m_spawnInterval = nativeInterval
};
InvokePatchedSpawn(spawnSystem, unadjustedLeech, eventSpawner: false, prefix, postfix);
ExpectTrue(spawnSystem.SuccessfulSpawns == 3, "unadjusted ordinary Leech can complete native spawning");
ExpectTrue(Diagnostics.Emitted.Count == 1, "unadjusted ordinary Leech emits no adjusted success event");

spawnSystem.ThrowOnSpawn = true;
try
{
    InvokePatchedSpawn(spawnSystem, adjustedLeech, eventSpawner: false, prefix, postfix);
    throw new InvalidOperationException("failed native spawn unexpectedly returned");
}
catch (TestSpawnException)
{
}
ExpectTrue(Diagnostics.Emitted.Count == 1, "failed native instantiation emits no success event");

SpawnSystem.m_nospawn = false;
ZNetScene.instance = null;

Console.WriteLine("leech spawn interval, idempotence, and successful-boundary checks passed");
return;

static void ExpectClose(float expected, float actual, string scenario)
{
    if (MathF.Abs(expected - actual) > 0.0001f)
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}

static void ExpectPopulation(string reason, int loadedCount, bool saturated)
{
    ExpectTrue(Diagnostics.Emitted.Count == 1, $"{reason} emits exactly one population event");
    DiagnosticEvent population = Diagnostics.Emitted[0];
    ExpectTrue(population.Name == "spawn_probe_population", $"{reason} uses the population event");
    ExpectTrue((string)population.Fields["reason"] == reason, $"{reason} records its transition reason");
    ExpectTrue((int)population.Fields["loaded_count"] == loadedCount, $"{reason} records loaded count");
    ExpectTrue((bool)population.Fields["configured_cap_saturated"] == saturated, $"{reason} records configured-cap saturation");
}

static void ExpectTrue(bool value, string scenario)
{
    if (!value)
    {
        throw new InvalidOperationException($"{scenario}: expected true");
    }
}

static void ExpectParameters(
    MethodInfo method,
    string[] expectedNames,
    Type[] expectedTypes,
    string scenario)
{
    ParameterInfo[] parameters = method.GetParameters();
    ExpectTrue(parameters.Select(parameter => parameter.Name).SequenceEqual(expectedNames), $"{scenario} names");
    ExpectTrue(parameters.Select(parameter => parameter.ParameterType).SequenceEqual(expectedTypes), $"{scenario} types");
}

static void InvokePatchedSpawn(
    SpawnSystem spawnSystem,
    SpawnSystem.SpawnData spawner,
    bool eventSpawner,
    MethodInfo prefix,
    MethodInfo postfix)
{
    object?[] prefixArguments = { spawner, eventSpawner, false };
    prefix.Invoke(null, prefixArguments);
    spawnSystem.InvokeSpawn(spawner, eventSpawner);
    postfix.Invoke(null, new[] { spawner, prefixArguments[2] });
}

static void InvokePostfix(object instance, string methodName)
{
    MethodInfo target = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        ?? throw new InvalidOperationException($"test target {methodName} is missing");
    target.Invoke(instance, Array.Empty<object>());

    MethodInfo postfix = typeof(LeechSpawnPatches).GetMethod(
        "SpawnSystemAwakePostfix",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("SpawnSystem Awake postfix is missing");
    postfix.Invoke(null, new[] { instance });
}

sealed class SpawnData
{
}
