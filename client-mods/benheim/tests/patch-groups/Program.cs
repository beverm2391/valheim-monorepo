using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.Broken
{
    [HarmonyPatch]
    internal static class BrokenValidPatch
    {
    }

    [HarmonyPatch]
    internal static class BrokenInvalidPatch
    {
    }
}

namespace BenheimQoL.Healthy
{
    [HarmonyPatch]
    internal static class HealthyPatch
    {
    }
}

namespace BenheimQoL.Split
{
    [HarmonyPatch]
    [PatchGroup("EnemyTiers.Map")]
    internal static class MapPatch
    {
    }

    [HarmonyPatch]
    [PatchGroup("EnemyTiers.Spawn")]
    internal static class SpawnPatch
    {
    }
}

namespace PatchGroupTests
{
    using BenheimQoL.Broken;
    using BenheimQoL.Healthy;
    using BenheimQoL.Split;

    internal static class Program
    {
        private static int Main()
        {
            List<string> failures = new List<string>();
            List<string> cleanupFailures = new List<string>();
            List<string> cleanupRecoveries = new List<string>();

            PatchGroupManager manager = PatchGroupManager.Apply(
                new[]
                {
                    typeof(BrokenValidPatch),
                    typeof(HealthyPatch),
                    typeof(BrokenInvalidPatch),
                },
                "com.benheim.patch-group-test",
                (owner, patchType, exception) =>
                    failures.Add($"{owner}|{patchType}|{exception.GetBaseException().Message}"),
                (owner, exception) => cleanupFailures.Add($"{owner}|{exception.Message}"),
                owner => cleanupRecoveries.Add(owner));

            Require(!manager.IsAvailable(typeof(BrokenValidPatch)), "the failing group is unavailable");
            Require(manager.IsAvailable(typeof(HealthyPatch)), "an unrelated group remains available");
            Require(!PatchState.IsApplied(typeof(BrokenValidPatch)),
                "the failing group removes its earlier partial patch");
            Require(PatchState.IsApplied(typeof(HealthyPatch)),
                "the unrelated group keeps its patch");
            Require(failures.Count == 1, "the failing group reports one exact failure");
            Require(failures[0].Contains("Broken|BenheimQoL.Broken.BrokenInvalidPatch|", StringComparison.Ordinal),
                "the failure names its owner and exact patch type");
            Require(cleanupFailures.Count == 0, "partial cleanup succeeds");
            Require(cleanupRecoveries.Count == 0, "no cleanup retry is needed");

            PatchGroupManager splitManager = PatchGroupManager.Apply(
                new[] { typeof(MapPatch), typeof(SpawnPatch) },
                "com.benheim.patch-group-test.split",
                (owner, patchType, exception) => throw exception,
                (owner, exception) => throw exception,
                owner => { });
            Require(splitManager.IsAvailable(typeof(MapPatch)), "an explicit map owner is available");
            Require(splitManager.IsAvailable(typeof(SpawnPatch)), "an explicit spawn owner is available");
            splitManager.UnpatchAll();

            manager.UnpatchAll();
            Require(!PatchState.IsApplied(typeof(HealthyPatch)),
                "normal teardown removes surviving groups");

            Console.WriteLine("patch groups contain partial failure and preserve unrelated patches");
            return 0;
        }

        private static void Require(bool condition, string scenario)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"failed: {scenario}");
            }
        }
    }
}
