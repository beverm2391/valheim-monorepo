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
Require(AffinityRules.ReadStoredValue("v1:test") == AffinityLoadResult.Test, "versioned Test Affinity must load");

Require(AffinityRules.IsEligibleWeapon(true, 4, 4, AffinityLoadResult.Lunge), "real Affinities require max quality");
Require(!AffinityRules.IsEligibleWeapon(true, 3, 4, AffinityLoadResult.Lunge), "real Affinities reject lower quality");
Require(AffinityRules.IsEligibleWeapon(true, 3, 4, AffinityLoadResult.Test), "Test Affinity accepts any native quality");
Require(!AffinityRules.IsEligibleWeapon(false, 4, 4, AffinityLoadResult.Test), "same-shaped noncanonical items stay unsupported");
Require(!AffinityRules.IsNativeWeapon(true, 0, 4), "quality below the native range must be rejected");
Require(!AffinityRules.IsNativeWeapon(true, 5, 4), "quality above the native range must be rejected");

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
AffinityRequirementSpec testRequirement = AffinityPresentation.RequirementsFor(AffinityLoadResult.Test);
Require(testRequirement.MaterialPrefab == "Wood" && testRequirement.MaterialAmount == 1,
    "Test Affinity must permanently cost 1 Wood");
Require(AffinityPresentation.BehaviorDescription(AffinityLoadResult.Test, 10f, 3f)
        == "Gameplay power: none. Test Affinity only confirms that you can apply an Affinity at the Forge.\nPersistent bias: none.",
    "Test Affinity must explicitly have no gameplay power or bias");

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

Require(AffinityRules.ReadStoredValue("v1:snipe") == AffinityLoadResult.Snipe, "versioned Snipe must load");
Require(AffinityRules.ReadStoredValue("v2:snipe") == AffinityLoadResult.Unsupported, "unknown Snipe versions must stay dormant");
Require(AffinityRules.IsSameAffinity(AffinityLoadResult.Snipe, AffinityLoadResult.Snipe), "installed Snipe must not replace itself");
AffinityRequirementSpec snipeRequirement = AffinityPresentation.RequirementsFor(AffinityLoadResult.Snipe);
Require(snipeRequirement.StationNameToken == "$piece_forge" && snipeRequirement.StationLevel == 1,
    "Snipe must use the level-1 native Forge");
Require(snipeRequirement.MaterialPrefab == "Wood" && snipeRequirement.MaterialAmount == 1,
    "Snipe must use the temporary 1 Wood cost");
string snipeTitle = AffinityPresentation.InventoryTitle("Huntsman bow", AffinityLoadResult.Snipe);
Require(snipeTitle == "Huntsman bow · Snipe", "Snipe item title must identify the affinity");
Require(AffinityPresentation.InventoryTitle(snipeTitle, AffinityLoadResult.Snipe) == snipeTitle,
    "Snipe item suffix must not repeat");
string snipeTooltip = AffinityPresentation.InventoryTooltip(nativeTooltip, AffinityLoadResult.Snipe, 10f, 3f);
Require(snipeTooltip.StartsWith(nativeTooltip, StringComparison.Ordinal), "Snipe must preserve native tooltip content");
Require(snipeTooltip.Contains("3x optical zoom", StringComparison.Ordinal)
    && snipeTooltip.Contains("25% longer", StringComparison.Ordinal)
    && snipeTooltip.Contains("1.25x through 20 m", StringComparison.Ordinal)
    && snipeTooltip.Contains("2.25x at 60 m", StringComparison.Ordinal)
    && snipeTooltip.Contains("1.75x at 40 m", StringComparison.Ordinal),
    "Snipe tooltip must explain scope, draw bias, and total distance curve");
Require(AffinityPresentation.InventoryTooltip(snipeTooltip, AffinityLoadResult.Snipe, 10f, 3f) == snipeTooltip,
    "Snipe tooltip must not repeat");

// Use the real state and runtime. The native boundary only supplies prefab
// identity, current equipment, and projectile lifetime; it makes no affinity decisions.
ObjectDB.instance = new ObjectDB();
var huntsmanPrefab = new UnityEngine.GameObject("BowHuntsman")
{
    Drop = new ItemDrop { m_itemData = new ItemDrop.ItemData { m_shared = new ItemDrop.SharedData { m_name = "$item_bow_huntsman" } } },
};
var clubPrefab = new UnityEngine.GameObject("Club")
{
    Drop = new ItemDrop { m_itemData = new ItemDrop.ItemData { m_shared = new ItemDrop.SharedData { m_name = "$item_club" } } },
};
ObjectDB.instance.Prefabs.Add("BowHuntsman", huntsmanPrefab);
ObjectDB.instance.Prefabs.Add("Club", clubPrefab);
ObjectDB.instance.Prefabs.Add("Wood", new UnityEngine.GameObject("Wood")
{
    Drop = new ItemDrop { m_itemData = new ItemDrop.ItemData { m_shared = new ItemDrop.SharedData { m_name = "$item_wood" } } },
});
var bow = new ItemDrop.ItemData
{
    m_dropPrefab = huntsmanPrefab,
    m_shared = new ItemDrop.SharedData { m_name = "$item_bow_huntsman" },
};
var otherBow = new ItemDrop.ItemData { m_dropPrefab = new UnityEngine.GameObject("BowFineWood") };
var spoofedBow = new ItemDrop.ItemData { m_dropPrefab = new UnityEngine.GameObject("BowHuntsman") };
var club = new ItemDrop.ItemData
{
    m_dropPrefab = clubPrefab,
    m_shared = new ItemDrop.SharedData { m_name = "$item_club" },
};
Require(AffinityState.IsEligibleForAffinity(bow, AffinityLoadResult.Snipe), "max-quality canonical Huntsman must accept Snipe");
Require(AffinityState.IsEligibleForAffinity(club, AffinityLoadResult.Lunge), "max-quality Club must accept Lunge");
Require(!AffinityState.IsEligibleSnipeBow(otherBow) && !AffinityState.IsEligibleSnipeBow(spoofedBow),
    "another bow or same-named noncanonical prefab must never qualify");
bow.m_quality = 3;
Require(!AffinityState.IsEligibleSnipeBow(bow), "lower-quality Huntsman must not accept a real Affinity");
Require(AffinityState.IsEligibleForAffinity(bow, AffinityLoadResult.Test), "lower-quality Huntsman must accept Test Affinity");
bow.m_quality = 5;
Require(!AffinityState.IsEligibleSnipeBow(bow), "above-max Huntsman must remain ineligible");
bow.m_quality = 4;
Require(!AffinityState.IsSnipe(bow), "eligible bow with no affinity must stay native");
AffinityState.Write(bow, AffinityLoadResult.Snipe, "test", false);
Require(AffinityState.StoredValue(bow) == "v1:snipe" && AffinityState.IsSnipe(bow),
    "Snipe writes versioned data on the exact eligible item");
AffinityState.Write(spoofedBow, AffinityLoadResult.Snipe, "test", false);
Require(!AffinityState.IsSnipe(spoofedBow), "stored Snipe alone must not activate on the wrong prefab");

var discoveryPlayer = new Player();
var unlocked = new System.Collections.Generic.List<AffinityCatalogEntry>();
AffinityCatalog.GetUnlocked(discoveryPlayer, unlocked);
Require(unlocked.Count == 0, "catalog must hide undiscovered weapon and material combinations");
discoveryPlayer.KnownRecipes.Add("$item_club");
discoveryPlayer.KnownMaterials.Add("$item_wood");
AffinityCatalog.GetUnlocked(discoveryPlayer, unlocked);
Require(unlocked.Count == 2
    && unlocked[0].Affinity == AffinityLoadResult.Lunge
    && unlocked[1].Affinity == AffinityLoadResult.Test,
    "discovering Club and Wood must reveal Lunge and Test once without owning a Club");
discoveryPlayer.KnownRecipes.Add("$item_bow_huntsman");
AffinityCatalog.GetUnlocked(discoveryPlayer, unlocked);
Require(unlocked.Count == 4, "each discovered supported weapon must reveal its real and Test entries once");
var firstClub = new ItemDrop.ItemData { m_dropPrefab = clubPrefab, m_quality = 1 };
var secondClub = new ItemDrop.ItemData { m_dropPrefab = clubPrefab, m_quality = 4 };
discoveryPlayer.Inventory.Items.Add(firstClub);
discoveryPlayer.Inventory.Items.Add(secondClub);
var ownedClubs = new System.Collections.Generic.List<ItemDrop.ItemData>();
AffinityCatalog.GetOwnedWeapons(discoveryPlayer, unlocked[0], ownedClubs);
Require(ownedClubs.Count == 2
    && ReferenceEquals(ownedClubs[0], firstClub)
    && ReferenceEquals(ownedClubs[1], secondClub),
    "catalog selection must preserve every exact owned weapon reference in inventory order");

// Exercise the shared application path used by both Forge and debug, with
// native boundaries stubbed and the actual eligibility/state/cost code linked.
var applicant = new Player { Station = new CraftingStation() };
Player.m_localPlayer = applicant;
foreach (var pair in new[] { (clubPrefab, AffinityLoadResult.Lunge), (huntsmanPrefab, AffinityLoadResult.Snipe) })
{
    for (int quality = 1; quality <= 4; quality++)
    {
        var forgeTarget = new ItemDrop.ItemData { m_dropPrefab = pair.Item1, m_quality = quality };
        applicant.Inventory.Items.Add(forgeTarget);
        applicant.Inventory.Wood = 2;
        var forgeResult = AffinityApplication.Apply(
            applicant, forgeTarget, pair.Item2, true, true, "test");
        if (quality < 4)
        {
            Require(forgeResult.Reason == "maximum_quality_required",
                $"normal {pair.Item2} application must reject quality {quality}");
            Require(applicant.Inventory.Wood == 2, "max-quality rejection must not charge");
        }
        else
        {
            Require(forgeResult.Applied, $"normal {pair.Item2} application must accept max quality");
            Require(applicant.Inventory.Wood == 1, "normal application spends the fixed 1 Wood cost");
            int calls = applicant.Inventory.RemoveCalls;
            Require(AffinityApplication.Apply(applicant, forgeTarget, pair.Item2, true, true, "test").Reason
                    == "affinity_already_installed",
                "normal same-affinity application must be rejected");
            Require(applicant.Inventory.RemoveCalls == calls, "same-affinity rejection must not charge");
        }

        var debugTarget = new ItemDrop.ItemData { m_dropPrefab = pair.Item1, m_quality = quality };
        applicant.Inventory.Items.Add(debugTarget);
        applicant.Inventory.Wood = 2;
        var debugResult = AffinityApplication.Apply(
            applicant,
            debugTarget,
            pair.Item2,
            requireForge: false,
            consumeResources: false,
            source: "test",
            developerBypass: true);
        Require(debugResult.Applied, $"debug apply must bypass max quality for {pair.Item2} at quality {quality}");
        Require(applicant.Inventory.Wood == 2, "debug apply must not spend resources");
        Require(
            pair.Item2 == AffinityLoadResult.Lunge
                ? AffinityState.IsLunge(debugTarget)
                : AffinityState.IsSnipe(debugTarget),
            "a debug-applied real Affinity must activate on every supported native quality");
        Require(AffinityApplication.Apply(
                applicant,
                debugTarget,
                pair.Item2,
                requireForge: false,
                consumeResources: false,
                source: "test",
                developerBypass: true).Applied,
            "debug apply must bypass replacement restrictions");
    }
}

foreach (UnityEngine.GameObject prefab in new[] { clubPrefab, huntsmanPrefab })
{
    for (int quality = 1; quality <= 4; quality++)
    {
        var target = new ItemDrop.ItemData { m_dropPrefab = prefab, m_quality = quality };
        applicant.Inventory.Items.Add(target);
        applicant.Inventory.Wood = 2;
        AffinityApplicationResult result = AffinityApplication.Apply(
            applicant, target, AffinityLoadResult.Test, true, true, "test");
        Require(result.Applied && applicant.Inventory.Wood == 1,
            $"Test Affinity must use the paid Forge flow at native quality {quality}");
        Require(AffinityState.Read(target) == AffinityLoadResult.Test
            && !AffinityState.IsLunge(target)
            && !AffinityState.IsSnipe(target),
            "Test Affinity must persist without activating Lunge or Snipe");
        int calls = applicant.Inventory.RemoveCalls;
        Require(AffinityApplication.Apply(applicant, target, AffinityLoadResult.Test, true, true, "test").Reason
                == "affinity_already_installed",
            "Test Affinity must reject normal reapplication");
        Require(applicant.Inventory.RemoveCalls == calls, "Test Affinity reapplication must not charge");
    }
}

var replacement = new ItemDrop.ItemData { m_dropPrefab = clubPrefab, m_quality = 4 };
applicant.Inventory.Items.Add(replacement);
applicant.Inventory.Wood = 3;
Require(AffinityApplication.Apply(applicant, replacement, AffinityLoadResult.Test, true, true, "test").Applied,
    "Test Affinity must apply before replacement");
AffinityApplicationResult replacementResult = AffinityApplication.Apply(
    applicant, replacement, AffinityLoadResult.Lunge, true, true, "test");
Require(replacementResult.Applied && replacementResult.Replacing && applicant.Inventory.Wood == 1,
    "paid real-Affinity replacement must consume the new fixed cost and replace Test Affinity");
foreach (var target in new[] {
    new ItemDrop.ItemData { m_dropPrefab = clubPrefab, m_quality = 0 },
    new ItemDrop.ItemData { m_dropPrefab = clubPrefab, m_quality = 5 },
    new ItemDrop.ItemData { m_dropPrefab = new UnityEngine.GameObject("Club") },
    otherBow, spoofedBow })
{
    applicant.Inventory.Items.Add(target);
    foreach (var affinity in new[] { AffinityLoadResult.Lunge, AffinityLoadResult.Snipe, AffinityLoadResult.Test })
        Require(AffinityApplication.Apply(applicant, target, affinity, true, true, "test").Reason == "ineligible_item",
            "unsupported prefab and non-native quality must fail both application paths");
    AffinityState.Write(target, AffinityLoadResult.Lunge, "test", false);
    Require(!AffinityState.IsLunge(target), "stored Lunge must not activate on an unsupported item");
}
var unpaid = new ItemDrop.ItemData { m_dropPrefab = clubPrefab, m_quality = 4 };
applicant.Inventory.Items.Add(unpaid);
applicant.Station = null;
Require(AffinityApplication.Apply(applicant, unpaid, AffinityLoadResult.Lunge, true, true, "test").Reason == "not_at_base_game_forge",
    "max quality must not bypass the Forge");
applicant.Station = new CraftingStation();
applicant.Inventory.Wood = 0;
Require(AffinityApplication.Apply(applicant, unpaid, AffinityLoadResult.Lunge, true, true, "test").Reason == "missing_resources",
    "max quality must not bypass resource cost");

var local = new Player { Weapon = bow };
Player.m_localPlayer = local;
Require(Math.Abs(SnipeRuntime.ClampDrawPercentage(1f, local) - 0.8f) < 0.0001f,
    "Snipe must remain at 80% at native full-draw time");
Require(SnipeRuntime.ClampDrawPercentage(1.25f, local) == 1f,
    "Snipe must reach full draw at 125% of skill-adjusted native duration");
Require(Math.Abs(SnipeRuntime.ClampDrawPercentage(0.5f, local) - 0.4f) < 0.0001f,
    "partial draw uses the longer draw duration");
Require(SnipeRuntime.ClampDrawPercentage(2f, local) == 1f && SnipeRuntime.ClampDrawPercentage(-1f, local) == 0f,
    "native clamp bounds must remain intact");
var remote = new Player { Weapon = bow };
Require(SnipeRuntime.ClampDrawPercentage(1f, remote) == 1f,
    "another player's draw must remain native locally");
Require(SnipeRuntime.ClampDrawPercentage(1f, new Humanoid()) == 1f,
    "non-player draw must remain native");
local.Weapon = otherBow;
Require(SnipeRuntime.ClampDrawPercentage(0.5f, local) == 0.5f,
    "ordinary equipped bows must retain native progress");
local.Weapon = bow;
var shot = new Projectile();
SnipeRuntime.ObserveShot(shot, local, bow);
Require(SnipeRuntime.IsSnipeShot(shot), "fired Snipe arrow must capture the firing affinity");
local.Weapon = otherBow;
Require(SnipeRuntime.IsSnipeShot(shot), "weapon swap must not alter an arrow already fired");
AffinityState.Clear(bow, "test");
Require(!AffinityState.IsSnipe(bow) && SnipeRuntime.IsSnipeShot(shot),
    "clearing the bow later must not alter a fired arrow's snapshot");
var ordinaryShot = new Projectile();
SnipeRuntime.ObserveShot(ordinaryShot, local, otherBow);
Require(!SnipeRuntime.IsSnipeShot(ordinaryShot), "ordinary arrows must remain native");
AffinityState.Write(bow, AffinityLoadResult.Snipe, "test", false);
SnipeRuntime.ObserveShot(ordinaryShot, new Character(), bow);
Require(!SnipeRuntime.IsSnipeShot(ordinaryShot), "non-player projectiles must remain native");
SnipeRuntime.ObserveShot(shot, local, otherBow);
Require(!SnipeRuntime.IsSnipeShot(shot), "reusing a projectile must discard its old snapshot");
BenheimQoL.Infrastructure.HealthReporting.GameplayActionsEnabled = false;
local.Weapon = bow;
Require(!SnipeRuntime.IsEquipped(local) && SnipeRuntime.ClampDrawPercentage(1f, local) == 1f,
    "disabled gameplay must leave draw native");
SnipeRuntime.ObserveShot(shot, local, bow);
Require(!SnipeRuntime.IsSnipeShot(shot), "disabled gameplay must not capture Snipe shots");
BenheimQoL.Infrastructure.HealthReporting.GameplayActionsEnabled = true;
Console.WriteLine("Affinity state, presentation, and Snipe runtime tests passed");
