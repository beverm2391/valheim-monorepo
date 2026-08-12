using System;
using System.IO;
using BepInEx.Configuration;
using BenheimQoL.CombatFeedback;

ExpectClose(0f, CombatFeedbackTuning.FocusReduction(0f), "no draw keeps native FOV");
ExpectClose(3.5f, CombatFeedbackTuning.FocusReduction(0.5f), "half draw reaches eased midpoint");
ExpectClose(7f, CombatFeedbackTuning.FocusReduction(1f), "full draw reaches the focus cap");
ExpectClose(7f, CombatFeedbackTuning.FocusReduction(2f), "draw above one stays capped");
ExpectClose(0f, CombatFeedbackTuning.FocusReduction(float.NaN), "invalid draw fails open to native FOV");

float previous = 0f;
for (int step = 1; step <= 100; step++)
{
    float reduction = CombatFeedbackTuning.FocusReduction(step / 100f);
    ExpectTrue(reduction >= previous, "focus curve stays monotonic");
    previous = reduction;
}

ExpectClose(0.45f, CombatFeedbackTuning.ShakeStrength(CombatFeedbackTrigger.Headshot), "headshot strength");
ExpectClose(1.75f, CombatFeedbackTuning.ShakeStrength(CombatFeedbackTrigger.Cleave), "cleave strength");
ExpectClose(1.75f, CombatFeedbackTuning.ShakeStrength(CombatFeedbackTrigger.MiningAoe), "mining AOE strength");
ExpectTrue(
    CombatFeedbackTuning.ShakeStrength(CombatFeedbackTrigger.Cleave)
        > CombatFeedbackTuning.NativeAxeHitShakeStrength,
    "cleave exceeds Valheim's ordinary axe-hit shake request");
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
    CombatFeedbackTuning.ShouldApplyShake(0.05f, activeStrength: 0.45f, requestedStrength: 1.75f),
    "stronger rapid shake replaces the active outcome");
ExpectTrue(
    CombatFeedbackTuning.ShouldApplyShake(CombatFeedbackTuning.ShakeCoalesceSeconds, 0.45f, 1.75f),
    "a later outcome can shake after the coalescing window");

string configPath = Path.Combine(Path.GetTempPath(), $"benheim-fx-{Guid.NewGuid():N}.cfg");
try
{
    ConfigFile config = new(configPath, saveOnInit: true);
    BenheimFxSettings.Initialize(config);
    ExpectTrue(BenheimFxSettings.MasterEnabled, "FX master defaults on");
    ExpectTrue(BenheimFxSettings.BowFocusEnabled, "bow focus defaults on");
    ExpectTrue(BenheimFxSettings.CombatShakeEnabled, "combat shake defaults on");
    ExpectTrue(BenheimFxSettings.DangerArrivalEnabled, "danger arrival FX defaults on");

    BenheimFxSettings.SetBowFocus(false);
    BenheimFxSettings.SetCombatShake(false);
    BenheimFxSettings.SetDangerArrival(false);
    BenheimFxSettings.SetMaster(false);
    ExpectTrue(!BenheimFxSettings.BowFocusEnabled, "master off suppresses bow focus");
    ExpectTrue(!BenheimFxSettings.CombatShakeEnabled, "master off suppresses combat shake");
    ExpectTrue(!BenheimFxSettings.DangerArrivalEnabled, "master off suppresses danger arrival FX");
    ExpectTrue(!BenheimFxSettings.BowFocusPreference, "master off preserves bow preference");
    ExpectTrue(!BenheimFxSettings.CombatShakePreference, "master off preserves shake preference");
    ExpectTrue(!BenheimFxSettings.DangerArrivalPreference, "master off preserves arrival preference");

    BenheimFxSettings.SetBowFocus(true);
    BenheimFxSettings.SetCombatShake(true);
    BenheimFxSettings.SetDangerArrival(true);
    ExpectTrue(!BenheimFxSettings.BowFocusEnabled, "master still overrides changed bow preference");
    ExpectTrue(!BenheimFxSettings.CombatShakeEnabled, "master still overrides changed shake preference");
    ExpectTrue(!BenheimFxSettings.DangerArrivalEnabled, "master still overrides changed arrival preference");

    ConfigFile reloaded = new(configPath, saveOnInit: true);
    BenheimFxSettings.Initialize(reloaded);
    ExpectTrue(!BenheimFxSettings.MasterEnabled, "master preference persists");
    ExpectTrue(BenheimFxSettings.BowFocusPreference, "bow preference persists behind master");
    ExpectTrue(BenheimFxSettings.CombatShakePreference, "shake preference persists behind master");
    ExpectTrue(BenheimFxSettings.DangerArrivalPreference, "arrival preference persists behind master");
    BenheimFxSettings.SetMaster(true);
    ExpectTrue(BenheimFxSettings.BowFocusEnabled, "master on restores saved bow preference");
    ExpectTrue(BenheimFxSettings.CombatShakeEnabled, "master on restores saved shake preference");
    ExpectTrue(BenheimFxSettings.DangerArrivalEnabled, "master on restores saved arrival preference");
}
finally
{
    if (File.Exists(configPath))
    {
        File.Delete(configPath);
    }
}

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
