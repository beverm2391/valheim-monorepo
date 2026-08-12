using System;
using BenheimQoL.WeaponRhythm;

ExpectTrue(
    AirborneMeleeRules.Qualifies(
        targetIsCharacter: true,
        attackerIsLocalPlayer: true,
        attackerIsAirborne: true),
    "local airborne melee contact qualifies");
ExpectFalse(
    AirborneMeleeRules.Qualifies(
        targetIsCharacter: true,
        attackerIsLocalPlayer: true,
        attackerIsAirborne: false),
    "grounded local melee contact stays native");
ExpectFalse(
    AirborneMeleeRules.Qualifies(
        targetIsCharacter: true,
        attackerIsLocalPlayer: false,
        attackerIsAirborne: true),
    "enemy or remote attack stays native");
ExpectFalse(
    AirborneMeleeRules.Qualifies(
        targetIsCharacter: false,
        attackerIsLocalPlayer: true,
        attackerIsAirborne: true),
    "destructible and terrain contact stays native");

ExpectNear(1.15f, AirborneMeleeTuning.DamageMultiplier, "damage tuning is modest");
ExpectNear(2f, AirborneMeleeTuning.StaggerMultiplier, "stagger tuning is substantially stronger");

Console.WriteLine("airborne melee qualification and tuning checks passed");
return;

static void ExpectTrue(bool actual, string scenario)
{
    if (!actual)
    {
        throw new InvalidOperationException($"{scenario}: expected true");
    }
}

static void ExpectFalse(bool actual, string scenario)
{
    if (actual)
    {
        throw new InvalidOperationException($"{scenario}: expected false");
    }
}

static void ExpectNear(float expected, float actual, string scenario)
{
    if (Math.Abs(expected - actual) > 0.0001f)
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}
