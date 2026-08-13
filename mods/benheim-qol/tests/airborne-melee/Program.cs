using System;
using BenheimQoL.WeaponRhythm;

ExpectTrue(
    AirborneMeleeRules.Qualifies(
        targetIsCharacter: true,
        attackerIsLocalPlayer: true,
        attackerIsGrounded: false,
        verticalSpeed: -0.5f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold,
        towardTargetSpeed: 7f,
        approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold),
    "local descending sprint-approach contact qualifies at both thresholds");
ExpectFalse(
    AirborneMeleeRules.Qualifies(
        targetIsCharacter: true,
        attackerIsLocalPlayer: true,
        attackerIsGrounded: true,
        verticalSpeed: -5f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold,
        towardTargetSpeed: 9f,
        approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold),
    "grounded local melee contact stays native");
ExpectFalse(
    AirborneMeleeRules.Qualifies(
        targetIsCharacter: true,
        attackerIsLocalPlayer: false,
        attackerIsGrounded: false,
        verticalSpeed: -5f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold,
        towardTargetSpeed: 9f,
        approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold),
    "enemy or remote attack stays native");
ExpectFalse(
    AirborneMeleeRules.Qualifies(
        targetIsCharacter: false,
        attackerIsLocalPlayer: true,
        attackerIsGrounded: false,
        verticalSpeed: -5f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold,
        towardTargetSpeed: 9f,
        approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold),
    "destructible and terrain contact stays native");
ExpectFalse(
    AirborneMeleeRules.Qualifies(
        targetIsCharacter: true,
        attackerIsLocalPlayer: true,
        attackerIsGrounded: false,
        verticalSpeed: 2f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold,
        towardTargetSpeed: 9f,
        approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold),
    "rising local melee contact stays native");
ExpectFalse(
    AirborneMeleeRules.Qualifies(
        targetIsCharacter: true,
        attackerIsLocalPlayer: true,
        attackerIsGrounded: false,
        verticalSpeed: -0.49f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold,
        towardTargetSpeed: 9f,
        approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold),
    "apex drift above the threshold stays native");

ExpectFalse(
    AirborneMeleeRules.Qualifies(
        targetIsCharacter: true,
        attackerIsLocalPlayer: true,
        attackerIsGrounded: false,
        verticalSpeed: -2f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold,
        towardTargetSpeed: 6.99f,
        approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold),
    "ordinary walk or jog jump stays native below the sprint band");
ExpectFalse(
    AirborneMeleeRules.Qualifies(
        targetIsCharacter: true,
        attackerIsLocalPlayer: true,
        attackerIsGrounded: false,
        verticalSpeed: -2f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold,
        towardTargetSpeed: -9f,
        approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold),
    "backward momentum stays native");

ExpectNear(8f, AirborneMeleeRules.ProjectPlanarVelocityToward(8f, 3f, 10f, 0f),
    "sideways speed does not inflate toward-target momentum");
ExpectNear(-8f, AirborneMeleeRules.ProjectPlanarVelocityToward(-8f, 3f, 10f, 0f),
    "backward speed remains negative");
ExpectNear(0f, AirborneMeleeRules.ProjectPlanarVelocityToward(8f, 3f, 0f, 0f),
    "degenerate overlapping contact fails closed");

ExpectNear(-0.5f, AirborneMeleeTuning.DescentThreshold, "descent threshold rejects apex jitter");
ExpectNear(7f, AirborneMeleeTuning.ApproachSpeedThreshold, "approach threshold requires native sprint-band momentum");
ExpectNear(1.15f, AirborneMeleeTuning.DamageMultiplier, "damage tuning stays modest");
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
