using System;
using System.Text.Json;
using BenheimQoL.Infrastructure;
using BenheimQoL.WeaponRhythm;

ExpectResolution(
    PerfectImpactResolution.Applied,
    AirborneMeleeRules.ResolveContact(
        attackerIsGrounded: false,
        verticalSpeed: -0.5f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold,
        towardTargetSpeed: 7f,
        approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold),
    "threshold contact qualifies");
ExpectResolution(
    PerfectImpactResolution.Grounded,
    AirborneMeleeRules.ResolveContact(
        attackerIsGrounded: true,
        verticalSpeed: -5f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold,
        towardTargetSpeed: 12f,
        approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold),
    "grounded contact stays native");
ExpectResolution(
    PerfectImpactResolution.RisingOrApex,
    AirborneMeleeRules.ResolveContact(
        attackerIsGrounded: false,
        verticalSpeed: -0.49f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold,
        towardTargetSpeed: 12f,
        approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold),
    "rising or apex contact stays native");
ExpectResolution(
    PerfectImpactResolution.InsufficientApproach,
    AirborneMeleeRules.ResolveContact(
        attackerIsGrounded: false,
        verticalSpeed: -5f,
        descentThreshold: AirborneMeleeTuning.DescentThreshold,
        towardTargetSpeed: 6.99f,
        approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold),
    "insufficient approach stays native");

ExpectNear(8f, AirborneMeleeRules.ProjectPlanarVelocityToward(8f, 3f, 10f, 0f),
    "sideways speed does not inflate approach");
ExpectNear(-8f, AirborneMeleeRules.ProjectPlanarVelocityToward(-8f, 3f, 10f, 0f),
    "backward speed remains negative");
ExpectNear(0f, AirborneMeleeRules.ProjectPlanarVelocityToward(0f, 8f, 10f, 0f),
    "pure sideways movement cannot qualify");
ExpectNear(0f, AirborneMeleeRules.ProjectPlanarVelocityToward(8f, 3f, 0f, 0f),
    "overlapping planar contact fails closed");

AirborneMeleeSwingState qualifyingSwing = new(
    "qualifying", "$item_club", "primary", "swing_longsword", "Horizontal");
ExpectTrue(
    qualifyingSwing.TryResolve(PerfectImpactResolution.Applied),
    "first Character contact resolves the attack");
ExpectTrue(qualifyingSwing.Qualified, "qualified attack retains its result");
ExpectFalse(
    qualifyingSwing.TryResolve(PerfectImpactResolution.Grounded),
    "later contact cannot reverse a qualified attack");

AirborneMeleeSwingState nativeSwing = new(
    "native", "$item_sword", "secondary", "sword_secondary", "Vertical");
ExpectTrue(
    nativeSwing.TryResolve(PerfectImpactResolution.InsufficientApproach),
    "first nonqualifying contact resolves the attack");
ExpectFalse(nativeSwing.Qualified, "ordinary attack remains native");
ExpectFalse(
    nativeSwing.TryResolve(PerfectImpactResolution.Applied),
    "later target cannot qualify an already resolved attack");

ExpectNear(-0.5f, AirborneMeleeTuning.DescentThreshold, "descent threshold rejects apex jitter");
ExpectNear(7f, AirborneMeleeTuning.ApproachSpeedThreshold, "approach threshold uses sprint-band momentum");
ExpectNear(1.15f, AirborneMeleeTuning.DamageMultiplier, "damage tuning stays modest");
ExpectNear(3f, AirborneMeleeTuning.StaggerMultiplier, "stagger tuning creates an opening");

PerfectImpactOutcome typedOutcome = new PerfectImpactOutcome(
    "impact-1",
    PerfectImpactResolution.Applied,
    "$item_sword",
    "primary",
    "swing_longsword",
    "Horizontal",
    "Swords",
    "Lox(Clone)",
    attackerGrounded: false,
    verticalSpeed: -1.5f,
    descentThreshold: -0.5f,
    towardTargetSpeed: 7.25f,
    approachThreshold: 7f,
    damageMultiplier: 1.15f,
    staggerMultiplier: 3f,
    feedback: "placed");
DiagnosticEvent typedEvent = PerfectImpactDiagnostics.CreateEvent(typedOutcome);
typedEvent.Prepare(
    new DateTime(2026, 8, 21, 4, 5, 6, DateTimeKind.Utc),
    "session-impact",
    "candidate");
using JsonDocument typedJson = JsonDocument.Parse(typedEvent.ToJsonLine());
JsonElement root = typedJson.RootElement;
ExpectText("WeaponRhythm", root, "domain", "typed outcome domain");
ExpectText("perfect_impact_outcome", root, "event", "typed outcome name");
ExpectText("impact-1", root, "operation_id", "typed outcome operation");
ExpectText("terminal", root, "operation_phase", "typed outcome phase");
ExpectTrue(root.GetProperty("qualified").GetBoolean(), "typed outcome qualification");
ExpectText("applied", root, "reason", "typed outcome reason");
ExpectText("$item_sword", root, "weapon", "typed outcome weapon");
ExpectText("primary", root, "attack_control", "typed outcome control");
ExpectText("swing_longsword", root, "attack_animation", "typed outcome animation");
ExpectText("Horizontal", root, "attack_type", "typed outcome type");
ExpectText("Swords", root, "skill", "typed outcome skill");
ExpectText("Lox(Clone)", root, "target", "typed outcome target");
ExpectFalse(root.GetProperty("attacker_grounded").GetBoolean(), "typed outcome ground state");
ExpectNear(-1.5f, root.GetProperty("vertical_speed").GetSingle(), "typed outcome descent");
ExpectNear(-0.5f, root.GetProperty("descent_threshold").GetSingle(), "typed descent threshold");
ExpectNear(7.25f, root.GetProperty("toward_target_speed").GetSingle(), "typed approach speed");
ExpectNear(7f, root.GetProperty("approach_threshold").GetSingle(), "typed approach threshold");
ExpectNear(1.15f, root.GetProperty("damage_multiplier").GetSingle(), "typed damage multiplier");
ExpectNear(3f, root.GetProperty("stagger_multiplier").GetSingle(), "typed stagger multiplier");
ExpectText("placed", root, "feedback", "typed feedback result");

int presentationCalls = 0;
int diagnosticCalls = 0;
int nativeDamageCalls = 0;
int failureReports = 0;
PerfectImpactOutcomeDelivery.Deliver(
    () =>
    {
        presentationCalls++;
        throw new InvalidOperationException("presentation failed");
    },
    () => diagnosticCalls++,
    () => nativeDamageCalls++,
    _ => failureReports++);
ExpectEqual(1, presentationCalls, "failing presentation runs once");
ExpectEqual(1, diagnosticCalls, "diagnostics continue after presentation failure");
ExpectEqual(1, nativeDamageCalls, "native damage continues after presentation failure");
ExpectEqual(1, failureReports, "presentation failure is reported once");

PerfectImpactOutcomeDelivery.Deliver(
    present: null,
    emitDiagnostic: () => throw new InvalidOperationException("diagnostics failed"),
    nativeDamage: () => nativeDamageCalls++,
    reportFailure: _ => throw new InvalidOperationException("reporting failed"));
ExpectEqual(2, nativeDamageCalls, "native damage survives diagnostic and reporter failures");

PerfectImpactOutcomeDelivery.Deliver(
    present: null,
    emitDiagnostic: () => diagnosticCalls++,
    nativeDamage: () => nativeDamageCalls++,
    reportFailure: _ => failureReports++);
ExpectEqual(3, nativeDamageCalls, "later area contact keeps its native damage call");

Console.WriteLine("Perfect Impact contact qualification and one-outcome checks passed");
return;

static void ExpectResolution(
    PerfectImpactResolution expected,
    PerfectImpactResolution actual,
    string scenario)
{
    if (expected != actual)
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}

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

static void ExpectText(string expected, JsonElement root, string property, string scenario)
{
    string? actual = root.GetProperty(property).GetString();
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}

static void ExpectEqual(int expected, int actual, string scenario)
{
    if (expected != actual)
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}
