using System;
using BenheimQoL.Archery;

ExpectClose(1.25f, HeadshotRules.DistanceMultiplier(0f), "point blank");
ExpectClose(1.25f, HeadshotRules.DistanceMultiplier(20f), "near boundary");
ExpectClose(1.375f, HeadshotRules.DistanceMultiplier(40f), "linear midpoint");
ExpectClose(1.50f, HeadshotRules.DistanceMultiplier(60f), "far boundary");
ExpectClose(1.50f, HeadshotRules.DistanceMultiplier(120f), "far cap");
ExpectClose(1.25f, HeadshotRules.DistanceMultiplier(float.NaN), "invalid distance fallback");

float ordinaryTolerance = HeadshotRules.HeadTolerance(
    struckDiameter: 0.8f,
    rootDiameter: 1.0f,
    rootHeight: 2.0f,
    creatureScale: 1.0f);
float scaledTolerance = HeadshotRules.HeadTolerance(
    struckDiameter: 0.8f,
    rootDiameter: 1.0f,
    rootHeight: 2.0f,
    creatureScale: 2.0f);
float tinyColliderTolerance = HeadshotRules.HeadTolerance(
    struckDiameter: 0.1f,
    rootDiameter: 1.0f,
    rootHeight: 2.0f,
    creatureScale: 1.0f);

ExpectTrue(ordinaryTolerance > 0f, "ordinary collider has a tolerance");
ExpectTrue(scaledTolerance > ordinaryTolerance, "creature scale expands tolerance");
ExpectTrue(tinyColliderTolerance < ordinaryTolerance, "struck collider dimensions constrain tolerance");
ExpectTrue(
    HeadshotRules.IsWithinTolerance(ordinaryTolerance, ordinaryTolerance),
    "boundary impact qualifies");
ExpectTrue(
    !HeadshotRules.IsWithinTolerance(ordinaryTolerance + 0.001f, ordinaryTolerance),
    "outside impact does not qualify");
ExpectClose(0f, HeadshotRules.HeadTolerance(0f, 1f, 2f, 1f), "invalid struck collider");

ExpectClose(
    4.5f,
    3f * 1.25f * HeadshotRules.CompensatedStaggerMultiplier(1.5f, 1.25f),
    "native stagger baseline at near distance");
ExpectClose(
    4.5f,
    3f * 1.50f * HeadshotRules.CompensatedStaggerMultiplier(1.5f, 1.50f),
    "native stagger baseline at far distance");

Console.WriteLine("headshot multiplier and geometry rules passed");
return;

static void ExpectClose(float expected, float actual, string scenario)
{
    if (MathF.Abs(expected - actual) > 0.0001f)
    {
        throw new InvalidOperationException(
            $"{scenario}: expected {expected}, got {actual}");
    }
}

static void ExpectTrue(bool value, string scenario)
{
    if (!value)
    {
        throw new InvalidOperationException($"{scenario}: expected true");
    }
}
