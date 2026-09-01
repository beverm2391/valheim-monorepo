using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    m_spawnInterval = nativeInterval
};
var spawnSystem = new SpawnSystem(adjustedLeech);
InvokePostfix(spawnSystem, "InvokeAwake");

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
