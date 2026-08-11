using System;
using BenheimQoL.CombatFeedback;

ExpectClose(0f, CombatFeedbackTuning.FocusReduction(0f), "no draw keeps native FOV");
ExpectClose(2.5f, CombatFeedbackTuning.FocusReduction(0.5f), "half draw reaches eased midpoint");
ExpectClose(5f, CombatFeedbackTuning.FocusReduction(1f), "full draw reaches the focus cap");
ExpectClose(5f, CombatFeedbackTuning.FocusReduction(2f), "draw above one stays capped");
ExpectClose(0f, CombatFeedbackTuning.FocusReduction(float.NaN), "invalid draw fails open to native FOV");

float previous = 0f;
for (int step = 1; step <= 100; step++)
{
    float reduction = CombatFeedbackTuning.FocusReduction(step / 100f);
    ExpectTrue(reduction >= previous, "focus curve stays monotonic");
    previous = reduction;
}

ExpectClose(0.45f, CombatFeedbackTuning.ShakeStrength(CombatFeedbackTrigger.Headshot), "headshot strength");
ExpectClose(0.32f, CombatFeedbackTuning.ShakeStrength(CombatFeedbackTrigger.Cleave), "cleave strength");
ExpectClose(0.38f, CombatFeedbackTuning.ShakeStrength(CombatFeedbackTrigger.MiningAoe), "mining AOE strength");
ExpectTrue(
    CombatFeedbackTuning.ShakeStrength(CombatFeedbackTrigger.Headshot) <= CombatFeedbackTuning.ShakeStrengthCap,
    "headshot respects the shared shake cap");
ExpectTrue(
    !CombatFeedbackTuning.ShouldApplyShake(0.05f, activeStrength: 0.45f, requestedStrength: 0.45f),
    "equal rapid shake is coalesced");
ExpectTrue(
    !CombatFeedbackTuning.ShouldApplyShake(0.05f, activeStrength: 0.45f, requestedStrength: 0.32f),
    "weaker rapid shake is coalesced");
ExpectTrue(
    CombatFeedbackTuning.ShouldApplyShake(0.05f, activeStrength: 0.32f, requestedStrength: 0.45f),
    "stronger rapid shake replaces the active outcome");
ExpectTrue(
    CombatFeedbackTuning.ShouldApplyShake(CombatFeedbackTuning.ShakeCoalesceSeconds, 0.45f, 0.32f),
    "a later outcome can shake after the coalescing window");

Console.WriteLine("combat feedback tuning and coalescing checks passed");
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
