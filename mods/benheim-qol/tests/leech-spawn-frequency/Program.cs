using System;
using BenheimQoL.Spawning;

var nativeInterval = 200f;
ExpectClose(200f / 3f, LeechSpawnFrequency.AdjustInterval(nativeInterval), "native interval is divided by three");
ExpectClose(0f, LeechSpawnFrequency.AdjustInterval(0f), "zero native interval remains zero");

var state = new LeechSpawnAdjustmentState<SpawnData>();
var shared = new SpawnData();
ExpectTrue(state.TryClaim(shared), "first shared SpawnData adjustment claims the reference");
ExpectTrue(!state.TryClaim(shared), "repeated shared SpawnData initialization is idempotent");
ExpectTrue(state.TryClaim(new SpawnData()), "a distinct SpawnData reference is independently claimable");

Console.WriteLine("leech spawn interval math and idempotence checks passed");
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

sealed class SpawnData
{
}
