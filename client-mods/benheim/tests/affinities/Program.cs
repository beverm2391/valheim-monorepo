using System;
using BenheimQoL.Affinities;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

Require(AffinityRules.ReadStoredValue(null) == AffinityLoadResult.None, "missing state must stay native");
Require(AffinityRules.ReadStoredValue("v1:lunge") == AffinityLoadResult.Lunge, "versioned Lunge must load");
Require(AffinityRules.ReadStoredValue("v2:lunge") == AffinityLoadResult.Unsupported, "unknown versions must stay dormant");

Require(AffinityRules.IsEligibleClub(true, 4, 4), "canonical max-quality Club must be eligible");
Require(!AffinityRules.IsEligibleClub(false, 4, 4), "same-shaped noncanonical item must be rejected");
Require(!AffinityRules.IsEligibleClub(true, 3, 4), "upgradable Club must be rejected");

Require(AffinityRules.CountConsumed(4, 3) == 1, "resource delta must capture one consumed item");
Require(AffinityRules.CountConsumed(4, 4) == 0, "unchanged resources must report no consumption");
Require(AffinityRules.CountConsumed(3, 4) == 0, "resource gains must never look consumed");

Require(AffinityRules.IsSameAffinity(AffinityLoadResult.Lunge, AffinityLoadResult.Lunge), "the installed Affinity must not replace itself");
Require(!AffinityRules.IsSameAffinity(AffinityLoadResult.None, AffinityLoadResult.Lunge), "an empty slot must accept Lunge");
Require(!AffinityRules.IsSameAffinity(AffinityLoadResult.Unsupported, AffinityLoadResult.Lunge), "a different stored Affinity may be replaced");

Require(AffinityRules.ResolveLunge(true, true, true, false, false, false, false) == "accepted", "valid airborne attempt must apply");
Require(AffinityRules.ResolveLunge(false, true, true, false, false, false, false) == "not_owner", "non-owner must be rejected");
Require(AffinityRules.ResolveLunge(true, false, true, false, false, false, false) == "weapon_changed", "changed weapon must be rejected");
Require(AffinityRules.ResolveLunge(true, true, false, false, false, false, false) == "affinity_missing", "missing state must be rejected");
Require(AffinityRules.ResolveLunge(true, true, true, true, false, false, false) == "grounded", "grounded swing must remain native");
Require(AffinityRules.ResolveLunge(true, true, true, false, true, false, false) == "swimming", "swimming must be rejected");
Require(AffinityRules.ResolveLunge(true, true, true, false, false, true, false) == "flying", "flying must be rejected");
Require(AffinityRules.ResolveLunge(true, true, true, false, false, false, true) == "attached", "attached must be rejected");

Require(AffinityRules.TryPlanarImpulse(3f, 4f, 10f, out float x, out float z), "planar forward must produce an impulse");
Require(Math.Abs(x - 6f) < 0.001f && Math.Abs(z - 8f) < 0.001f, "impulse must be normalized and scaled once");
Require(!AffinityRules.TryPlanarImpulse(0f, 0f, 10f, out _, out _), "zero planar forward must be rejected");
Require(Math.Abs(AffinityRules.RequiredVerticalImpulse(-2f, 3f) - 5f) < 0.001f, "falling Lunge must rise to the minimum vertical velocity");
Require(Math.Abs(AffinityRules.RequiredVerticalImpulse(1f, 3f) - 2f) < 0.001f, "rising Lunge must reach the minimum vertical velocity");
Require(Math.Abs(AffinityRules.RequiredVerticalImpulse(4f, 3f)) < 0.001f, "Lunge must not reduce an existing upward velocity");

Console.WriteLine("Affinity rule tests passed");
