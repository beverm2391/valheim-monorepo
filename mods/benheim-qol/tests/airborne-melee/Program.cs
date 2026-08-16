using System;
using BenheimQoL.WeaponRhythm;

ExpectTrue(
    AirborneMeleeRules.CanArm(
        attackerIsLocalPlayer: true,
        meleeAttack: true,
        attackerIsGrounded: false,
        forwardSpeed: 7f,
        forwardSpeedThreshold: AirborneMeleeTuning.ForwardSpeedThreshold),
    "airborne local melee arms at native sprint-band forward momentum");
ExpectFalse(
    AirborneMeleeRules.CanArm(
        attackerIsLocalPlayer: true,
        meleeAttack: true,
        attackerIsGrounded: true,
        forwardSpeed: 9f,
        forwardSpeedThreshold: AirborneMeleeTuning.ForwardSpeedThreshold),
    "grounded attack start never arms");
ExpectFalse(
    AirborneMeleeRules.CanArm(
        attackerIsLocalPlayer: true,
        meleeAttack: true,
        attackerIsGrounded: false,
        forwardSpeed: 6.99f,
        forwardSpeedThreshold: AirborneMeleeTuning.ForwardSpeedThreshold),
    "walk or jog momentum never arms");
ExpectFalse(
    AirborneMeleeRules.CanArm(
        attackerIsLocalPlayer: false,
        meleeAttack: true,
        attackerIsGrounded: false,
        forwardSpeed: 9f,
        forwardSpeedThreshold: AirborneMeleeTuning.ForwardSpeedThreshold),
    "remote or enemy attack never arms");
ExpectFalse(
    AirborneMeleeRules.CanArm(
        attackerIsLocalPlayer: true,
        meleeAttack: false,
        attackerIsGrounded: false,
        forwardSpeed: 9f,
        forwardSpeedThreshold: AirborneMeleeTuning.ForwardSpeedThreshold),
    "projectile and non-melee attacks never arm");

ExpectTrue(
    AirborneMeleeRules.CanConsume(
        armed: true,
        targetIsCharacter: true,
        attackerIsLocalPlayer: true,
        attackerIsGrounded: false,
        verticalSpeed: -0.5f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold),
    "armed swing consumes on an airborne descending Character hit");
ExpectFalse(
    AirborneMeleeRules.CanConsume(
        armed: false,
        targetIsCharacter: true,
        attackerIsLocalPlayer: true,
        attackerIsGrounded: false,
        verticalSpeed: -5f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold),
    "unarmed swing stays native even with a strong descent");
ExpectFalse(
    AirborneMeleeRules.CanConsume(
        armed: true,
        targetIsCharacter: true,
        attackerIsLocalPlayer: true,
        attackerIsGrounded: true,
        verticalSpeed: -5f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold),
    "landing before contact rejects consumption");
ExpectFalse(
    AirborneMeleeRules.CanConsume(
        armed: true,
        targetIsCharacter: true,
        attackerIsLocalPlayer: true,
        attackerIsGrounded: false,
        verticalSpeed: -0.49f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold),
    "rising or apex contact rejects consumption");
ExpectFalse(
    AirborneMeleeRules.CanConsume(
        armed: true,
        targetIsCharacter: false,
        attackerIsLocalPlayer: true,
        attackerIsGrounded: false,
        verticalSpeed: -5f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold),
    "destructible and terrain contact never consumes the swing");

ExpectNear(8f, AirborneMeleeRules.ProjectPlanarVelocityToward(8f, 3f, 10f, 0f),
    "sideways speed does not inflate physical forward momentum");
ExpectNear(-8f, AirborneMeleeRules.ProjectPlanarVelocityToward(-8f, 3f, 10f, 0f),
    "backward momentum remains negative");
ExpectNear(0f, AirborneMeleeRules.ProjectPlanarVelocityToward(0f, 8f, 10f, 0f),
    "pure sideways momentum cannot arm");

AirborneMeleeSwingState qualifyingSwing = new(
    "qualifying",
    startVerticalSpeed: 2f,
    startForwardSpeed: 8f);
ExpectTrue(qualifyingSwing.Resolve(qualified: true), "first Character contact resolves the swing");
ExpectTrue(qualifyingSwing.Qualified, "qualified result remains available to the synchronous area outcome");
ExpectFalse(qualifyingSwing.Resolve(qualified: true), "later area contacts cannot present or log again");

AirborneMeleeSwingState consumeRejectedSwing = new(
    "rejected",
    startVerticalSpeed: -2f,
    startForwardSpeed: 3f);
ExpectTrue(consumeRejectedSwing.Resolve(qualified: false), "first rejected Character contact resolves the swing");
ExpectFalse(consumeRejectedSwing.Qualified, "a rejected swing cannot become qualified on a later target");
ExpectFalse(consumeRejectedSwing.Resolve(qualified: true), "later target cannot reverse the terminal decision");

ExpectNear(-0.5f, AirborneMeleeTuning.DescentThreshold, "descent threshold rejects apex jitter");
ExpectNear(7f, AirborneMeleeTuning.ForwardSpeedThreshold, "arm threshold requires native sprint-band momentum");
ExpectNear(1.15f, AirborneMeleeTuning.DamageMultiplier, "damage tuning stays modest");
ExpectNear(3f, AirborneMeleeTuning.StaggerMultiplier, "stagger tuning creates the committed approach opening");

Console.WriteLine("airborne melee arming, consumption, and tuning checks passed");
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
