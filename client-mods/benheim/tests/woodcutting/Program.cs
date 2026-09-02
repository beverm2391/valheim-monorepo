using System;
using System.Linq;
using BenheimQoL.CombatFeedback;
using BenheimQoL.Infrastructure;
using BenheimQoL.Woodcutting;
using UnityEngine;

// Both entry postfixes run the production Cleave path. The engine side is a
// bounded fixture, so this proves dispatch decisions, not live gameplay acceptance.
foreach (Component target in new Component[] { new TreeBase(), new TreeLog() })
{
    Reset();
    target.View.OnDamage = target.View.ResetZDO;
    Damage(target, Hit());
    Expect(target && target.View && !target.View.IsValid(), "component remains alive after synchronous ZDO reset");
    Expect(target.View.Calls.Count == 1, "lethal primary hit sends no extra damage request");
    Expect(Diagnostics.Events.Any(e => e.Contains("reason=invalid_network_view")), "invalid target has bounded skip evidence");
    Expect(DamageText.instance.Shown == 0 && CombatFeedbackController.Shakes == 0, "skipped cleave has no success feedback");
}

// Verify the fixture really exposes the native dereference that the guard avoids.
TreeBase destroyedTree = new();
destroyedTree.View.ResetZDO();
ExpectThrows<NullReferenceException>(() => destroyedTree.Damage(Hit()));

foreach (Component target in new Component[] { new TreeBase(), new TreeLog() })
{
    Reset();
    // Primary delivery can change authority; the extra call must use the view's
    // current owner, with no local-owner restriction or ownership claim.
    target.View.OnDamage = () => target.View.Data!.Owner = 2;
    HitData original = Hit();
    Damage(target, original);
    Expect(target.View.Calls.Count == 2, "one original plus one same-target extra hit, with no recursion");
    Expect(target.View.Calls[1].Owner == 2, "extra damage follows current owner");
    HitData extra = target.View.Calls[1].Hit;
    Expect(!ReferenceEquals(original, extra), "extra hit is a clone");
    Expect(extra.m_damage.m_chop == 10 && extra.m_damage.m_slash == 5, "all extra damage is scaled to 50 percent");
    Expect(extra.m_attacker == original.m_attacker && extra.m_point == original.m_point, "extra keeps attacker and impact");
    Expect(extra.m_pushForce == 0 && extra.m_radius == 0 && extra.m_skillRaiseAmount == 0, "extra has no push, radius, or skill gain");
    Expect(original.m_damage.m_chop == 20 && original.m_pushForce == 4, "primary hit remains unchanged");
    Expect(DamageText.instance.Shown == 1 && CombatFeedbackController.Shakes == 1, "one cleave feedback");
    Expect(Diagnostics.Events.Single().Contains("damage_call_completed=true"), "diagnostic reports call completion only");
}

// A successful extra hit may itself destroy the target. It must finish and release
// the recursion guard without trying to reacquire a replacement log or tree.
Reset();
TreeBase cleaveKill = new();
cleaveKill.View.OnDamage = () =>
{
    if (cleaveKill.View.Calls.Count == 2) cleaveKill.View.ResetZDO();
};
Damage(cleaveKill, Hit());
Expect(cleaveKill.View.Calls.Count == 2 && DamageText.instance.Shown == 1, "native extra-hit destruction is preserved");

Reset();
TreeBase failing = new();
failing.View.OnDamage = () => throw new InvalidOperationException("unrelated native failure");
ExpectThrows<InvalidOperationException>(() => WoodcuttingProgression.TryApplyCleave(failing, Hit()));
Expect(Diagnostics.Events.Single().Contains("damage_call_completed=false"), "failed call remains visibly failed");
Expect(DamageText.instance.Shown == 0, "failed call has no success feedback");
TreeBase next = new();
WoodcuttingProgression.TryApplyCleave(next, Hit());
Expect(next.View.Calls.Count == 1, "unrelated failure cannot leave cleave recursion guard stuck");

foreach ((float skill, float roll, bool cleave) in new[]
{
    (0.249f, 0f, false), (0.25f, 0.3f, true), (0.25f, 0.301f, false),
    (1f, 0.85f, true), (1f, 0.851f, false)
})
{
    Reset();
    Player.m_localPlayer.SkillFactor = skill;
    UnityEngine.Random.value = roll;
    TreeBase target = new();
    Damage(target, Hit());
    Expect(target.View.Calls.Count == (cleave ? 2 : 1), "unlock and chance boundaries remain unchanged");
}

Console.WriteLine("woodcutting production-path lifecycle, extra-hit, routing, and balance checks passed (engine fixture)");

static HitData Hit() => new()
{
    m_attacker = 1,
    m_damage = new HitData.DamageTypes { m_chop = 20, m_slash = 10 },
    m_pushForce = 4, m_radius = 2, m_skillRaiseAmount = 1,
    m_point = new Vector3(1, 2, 3)
};

static void Damage(Component target, HitData hit)
{
    if (target is TreeBase tree) tree.Damage(hit);
    else ((TreeLog)target).Damage(hit);
}

static void Reset()
{
    Diagnostics.Events.Clear();
    DamageText.instance.Shown = 0;
    CombatFeedbackController.Shakes = 0;
    Player.m_localPlayer.SkillFactor = 0.25f;
    UnityEngine.Random.value = 0;
}

static void Expect(bool condition, string scenario)
{
    if (!condition) throw new Exception(scenario);
}

static void ExpectThrows<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}
