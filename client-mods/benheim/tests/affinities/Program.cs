using System;
using BenheimQoL.Affinities;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static int Count(string value, string fragment)
{
    int count = 0;
    int offset = 0;
    while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
    {
        count++;
        offset += fragment.Length;
    }
    return count;
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

AffinityRequirementSpec lungeRequirement = AffinityPresentation.RequirementsFor(AffinityLoadResult.Lunge);
Require(lungeRequirement.StationNameToken == "$piece_forge", "Lunge must require the native Forge");
Require(lungeRequirement.StationLevel == 1, "Lunge must require Forge level 1");
Require(lungeRequirement.MaterialPrefab == "Wood" && lungeRequirement.MaterialAmount == 1, "Lunge must keep the temporary 1 Wood cost");

const string nativeTitle = "Club";
const string nativeTooltip = "$item_club_description\n$item_weight: <color=orange>2.0</color>";
Require(
    AffinityPresentation.InventoryTitle(nativeTitle, AffinityLoadResult.None) == nativeTitle,
    "a native Club title must remain unchanged");
Require(
    AffinityPresentation.InventoryTooltip(nativeTooltip, AffinityLoadResult.None, 10f, 3f) == nativeTooltip,
    "a native Club tooltip must remain unchanged");
Require(
    AffinityPresentation.InventoryTitle(nativeTitle, AffinityLoadResult.Unsupported) == nativeTitle,
    "an item with unknown affinity data must keep its native title");
Require(
    AffinityPresentation.InventoryTooltip(nativeTooltip, AffinityLoadResult.Unsupported, 10f, 3f) == nativeTooltip,
    "an item with unknown affinity data must keep its native tooltip");

string affinityTitle = AffinityPresentation.InventoryTitle(nativeTitle, AffinityLoadResult.Lunge);
Require(affinityTitle == "Club · Lunge", "the exact Lunge Club must identify its Affinity");
Require(
    AffinityPresentation.InventoryTitle(affinityTitle, AffinityLoadResult.Lunge) == affinityTitle,
    "the Lunge title suffix must not repeat");

string affinityTooltip = AffinityPresentation.InventoryTooltip(
    nativeTooltip,
    AffinityLoadResult.Lunge,
    10f,
    3f);
Require(affinityTooltip.StartsWith(nativeTooltip, StringComparison.Ordinal), "the native Club tooltip must be preserved first");
Require(affinityTooltip.Contains("Every airborne primary swing adds 10 m/s to your forward velocity", StringComparison.Ordinal), "the tooltip must state Lunge's actual movement");
Require(affinityTooltip.Contains("at least +3 m/s", StringComparison.Ordinal), "the tooltip must state Lunge's vertical floor");
Require(affinityTooltip.Contains("Persistent bias:", StringComparison.Ordinal), "the tooltip must state Lunge's persistent bias");
Require(
    AffinityPresentation.InventoryTooltip(nativeTooltip, AffinityLoadResult.Lunge, 12.5f, 3f)
        .Contains("12.5 m/s to your forward velocity", StringComparison.Ordinal),
    "the tooltip must reflect a session force override");
string recomposedTooltip = AffinityPresentation.InventoryTooltip(
    affinityTooltip,
    AffinityLoadResult.Lunge,
    10f,
    3f);
Require(recomposedTooltip == affinityTooltip, "the Affinity section must not repeat");
Require(Count(recomposedTooltip, "Affinity: Lunge") == 1, "the Affinity heading must appear exactly once");

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
