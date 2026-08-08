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
float largeHeadColliderTolerance = HeadshotRules.HeadTolerance(
    struckDiameter: 3.0f,
    rootDiameter: 3.0f,
    rootHeight: 3.5f,
    creatureScale: 1.0f,
    struckColliderContainsHead: true);
float humanoidHeadColliderTolerance = HeadshotRules.HeadTolerance(
    struckDiameter: 0.45f,
    rootDiameter: 1.0f,
    rootHeight: 2.0f,
    creatureScale: 1.0f,
    struckColliderContainsHead: true);
float smallCreatureHeadColliderTolerance = HeadshotRules.HeadTolerance(
    struckDiameter: 0.3f,
    rootDiameter: 0.8f,
    rootHeight: 1.2f,
    creatureScale: 0.4f,
    struckColliderContainsHead: true);
float broadChildColliderTolerance = HeadshotRules.HeadTolerance(
    struckDiameter: 10.0f,
    rootDiameter: 1.0f,
    rootHeight: 2.0f,
    creatureScale: 1.0f,
    struckColliderContainsHead: true);

ExpectTrue(ordinaryTolerance > 0f, "ordinary collider has a tolerance");
ExpectTrue(scaledTolerance > ordinaryTolerance, "creature scale expands tolerance");
ExpectTrue(tinyColliderTolerance < ordinaryTolerance, "struck collider dimensions constrain tolerance");
ExpectTrue(
    MathF.Abs(largeHeadColliderTolerance - 0.9f) < 0.0001f,
    "large head collider may replace the height cap but remains root-width capped");
ExpectTrue(
    MathF.Abs(humanoidHeadColliderTolerance - 0.3f) < 0.0001f,
    "humanoid head collider uses the owning character root-width cap");
ExpectTrue(
    smallCreatureHeadColliderTolerance < humanoidHeadColliderTolerance,
    "small scaled creature receives a smaller world-space head tolerance");
ExpectTrue(
    MathF.Abs(broadChildColliderTolerance - 0.3f) < 0.0001f,
    "broad child collider cannot bypass the owning character root-width cap");
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
