using System;
using BenheimQoL.EnemyTiers;

const float WorldSize = 10_000f;
var exampleBiome = new BiomeChanceCurve(minimumChance: 8f, maximumChance: 16f);

ExpectClose(0f, WildernessStarChance.NormalizeDistance(0f, WorldSize), "center normalized distance");
ExpectClose(0.5f, WildernessStarChance.NormalizeDistance(5_000f, WorldSize), "midpoint normalized distance");
ExpectClose(1f, WildernessStarChance.NormalizeDistance(WorldSize, WorldSize), "world-edge normalized distance");
ExpectClose(0f, WildernessStarChance.NormalizeDistance(-100f, WorldSize), "negative distance clamps to center");
ExpectClose(1f, WildernessStarChance.NormalizeDistance(12_000f, WorldSize), "distance beyond world edge clamps");

ExpectClose(0f, WildernessStarChance.GlobalDistanceAddition(0f), "center global addition");
ExpectClose(5f, WildernessStarChance.GlobalDistanceAddition(0.5f), "midpoint global addition");
ExpectClose(10f, WildernessStarChance.GlobalDistanceAddition(1f), "world-edge global addition");
ExpectClose(0f, WildernessStarChance.GlobalDistanceAddition(-1f), "negative global-addition input clamps");
ExpectClose(10f, WildernessStarChance.GlobalDistanceAddition(2f), "global-addition input beyond edge clamps");

ExpectClose(8f, exampleBiome.ChanceAt(-1f), "biome curve clamps below center");
ExpectClose(8f, exampleBiome.ChanceAt(0f), "biome curve center");
ExpectClose(12f, exampleBiome.ChanceAt(0.5f), "biome curve midpoint");
ExpectClose(16f, exampleBiome.ChanceAt(1f), "biome curve world edge");
ExpectClose(16f, exampleBiome.ChanceAt(2f), "biome curve clamps beyond world edge");

ExpectClose(
    8f,
    WildernessStarChance.AdjustEffectiveChance(10f, 1f, exampleBiome, 0f, WorldSize),
    "biome minimum applies at center");
ExpectClose(
    17f,
    WildernessStarChance.AdjustEffectiveChance(10f, 1f, exampleBiome, 5_000f, WorldSize),
    "biome midpoint adds global distance");
ExpectClose(
    26f,
    WildernessStarChance.AdjustEffectiveChance(10f, 1f, exampleBiome, WorldSize, WorldSize),
    "biome maximum adds world-edge term");

ExpectClose(
    4.92f,
    WildernessStarChance.GlobalDistanceAddition(WildernessStarChance.NormalizeDistance(4_920f, WorldSize)),
    "Bonemass-distance global calibration");
ExpectClose(
    7.882f,
    WildernessStarChance.GlobalDistanceAddition(WildernessStarChance.NormalizeDistance(7_882f, WorldSize)),
    "known-frontier global calibration");

ExpectClose(
    0f,
    WildernessStarChance.AdjustEffectiveChance(0f, 1f, exampleBiome, WorldSize, WorldSize),
    "native zero chance remains zero");
ExpectClose(
    26f,
    WildernessStarChance.AdjustEffectiveChance(35f, 1f, exampleBiome, WorldSize, WorldSize),
    "native chance above the previous cap does not bypass the resolved formula");
ExpectClose(
    26f,
    WildernessStarChance.AdjustEffectiveChance(10f, 2f, exampleBiome, WorldSize, WorldSize),
    "biome composition defines the final chance");

var maximumBiome = new BiomeChanceCurve(30f, 30f);
ExpectClose(40f, WildernessStarChance.AdjustEffectiveChance(10f, 1f, maximumBiome, WorldSize, WorldSize), "constructed maximum has no hard cap");

ExpectTrue(WildernessDangerScale.Classify(0f) == WildernessDanger.Safe, "zero pressure is safe");
ExpectTrue(WildernessDangerScale.Classify(17.499f) == WildernessDanger.Safe, "safe upper edge");
ExpectTrue(WildernessDangerScale.Classify(17.5f) == WildernessDanger.Sketchy, "sketchy lower edge");
ExpectTrue(WildernessDangerScale.Classify(24.999f) == WildernessDanger.Sketchy, "sketchy upper edge");
ExpectTrue(WildernessDangerScale.Classify(25f) == WildernessDanger.Dangerous, "dangerous lower edge");
ExpectTrue(WildernessDangerScale.Classify(32.499f) == WildernessDanger.Dangerous, "dangerous upper edge");
ExpectTrue(WildernessDangerScale.Classify(32.5f) == WildernessDanger.Deadly, "deadly lower edge");
ExpectTrue(
    WildernessDangerScale.StyledLabel(WildernessDanger.Safe) == "<color=#6F9F6A>SAFE</color>",
    "safe label uses calm color");
ExpectTrue(
    WildernessDangerScale.StyledLabel(WildernessDanger.Sketchy) == "<color=#B59A45>SKETCHY</color>",
    "sketchy label uses warning color");
ExpectTrue(
    WildernessDangerScale.StyledLabel(WildernessDanger.Dangerous) == "<color=#C8753B><b>DANGEROUS</b></color>",
    "dangerous label uses bold orange treatment");
ExpectTrue(
    WildernessDangerScale.StyledLabel(WildernessDanger.Deadly) == "<color=#C94F55><b>DEADLY</b></color>",
    "deadly label uses bold red treatment");
ExpectTrue(
    WildernessDangerScale.StyledMapLabel(WildernessDanger.Safe) == "<b><color=#6F9F6A>SAFE</color></b>",
    "map label makes safe full-weight without changing its color");
ExpectTrue(
    WildernessDangerScale.StyledMapLabel(WildernessDanger.Deadly) == "<b><color=#C94F55><b>DEADLY</b></color></b>",
    "map label preserves deadly color and emphasis");
ExpectTrue(!WildernessDangerScale.IsVisible(false, false, false), "unexplored point stays hidden");
ExpectTrue(WildernessDangerScale.IsVisible(true, false, false), "locally explored point is visible");
ExpectTrue(!WildernessDangerScale.IsVisible(false, true, false), "hidden shared exploration stays hidden");
ExpectTrue(WildernessDangerScale.IsVisible(false, true, true), "enabled shared exploration is visible");
ExpectTrue(WildernessMapLabelLayout.IsResolvedNativeBiomeText("Meadows"), "localized biome name is resolved");
ExpectTrue(!WildernessMapLabelLayout.IsResolvedNativeBiomeText(""), "empty biome label is unresolved");
ExpectTrue(!WildernessMapLabelLayout.IsResolvedNativeBiomeText("   "), "blank biome label is unresolved");
ExpectTrue(!WildernessMapLabelLayout.IsResolvedNativeBiomeText("[biome_none]"), "raw localization token is unresolved");

var transitions = new WildernessDangerTransitionTracker();
WildernessDangerTransition baseline = transitions.Observe(16f, now: 0f, presentationAvailable: true);
ExpectTrue(baseline.BaselineEstablished, "login establishes a silent danger baseline");
ExpectTrue(baseline.ArrivalDanger == null, "login baseline does not present an arrival");
ExpectTrue(transitions.StableDanger == WildernessDanger.Safe, "safe login baseline becomes current");

WildernessDangerTransition dangerCandidate = transitions.Observe(26f, now: 1f, presentationAvailable: true);
ExpectTrue(dangerCandidate.CandidateStarted, "danger escalation starts debounce");
ExpectTrue(!transitions.Observe(26f, now: 2.99f, presentationAvailable: true).StableChanged, "danger does not stabilize before debounce");
WildernessDangerTransition dangerousArrival = transitions.Observe(26f, now: 3f, presentationAvailable: true);
ExpectTrue(dangerousArrival.StableChanged, "danger stabilizes after debounce");
ExpectTrue(dangerousArrival.ArrivalDanger == WildernessDanger.Dangerous, "stable dangerous escalation presents once");
ExpectTrue(transitions.Observe(26f, now: 4f, presentationAvailable: true).ArrivalDanger == null, "remaining dangerous does not repeat arrival");

ExpectTrue(
    !transitions.Observe(24.5f, now: 5f, presentationAvailable: true).CandidateStarted,
    "hysteresis ignores a small dangerous-boundary retreat");
WildernessDangerTransition retreatCandidate = transitions.Observe(24f, now: 6f, presentationAvailable: true);
ExpectTrue(retreatCandidate.CandidateStarted, "crossing beyond hysteresis starts retreat debounce");
WildernessDangerTransition cancelledRetreat = transitions.Observe(26f, now: 7f, presentationAvailable: true);
ExpectTrue(cancelledRetreat.CandidateCancelled, "returning before debounce cancels boundary retreat");

transitions.Observe(24f, now: 8f, presentationAvailable: true);
WildernessDangerTransition stableRetreat = transitions.Observe(24f, now: 10f, presentationAvailable: true);
ExpectTrue(stableRetreat.StableChanged, "retreat stabilizes after debounce");
transitions.Observe(26f, now: 11f, presentationAvailable: true);
WildernessDangerTransition cooldownReentry = transitions.Observe(26f, now: 13f, presentationAvailable: true);
ExpectTrue(cooldownReentry.ArrivalBlock == WildernessDangerArrivalBlock.Cooldown, "arrival cooldown rejects quick reentry");
ExpectTrue(cooldownReentry.ArrivalDanger == null, "cooldown rejection does not present");

var pausedTransition = new WildernessDangerTransitionTracker();
pausedTransition.Observe(16f, now: 0f, presentationAvailable: true);
pausedTransition.Observe(26f, now: 1f, presentationAvailable: true);
pausedTransition.PauseObservation();
WildernessDangerTransition resumedCandidate = pausedTransition.Observe(26f, now: 20f, presentationAvailable: true);
ExpectTrue(resumedCandidate.CandidateStarted, "resume restarts a pending transition debounce");
ExpectTrue(
    !pausedTransition.Observe(26f, now: 21.99f, presentationAvailable: true).StableChanged,
    "paused time does not satisfy transition debounce");
ExpectTrue(
    pausedTransition.Observe(26f, now: 22f, presentationAvailable: true).ArrivalDanger == WildernessDanger.Dangerous,
    "resumed danger must remain stable for a full debounce before arrival");

var unavailablePresentation = new WildernessDangerTransitionTracker();
unavailablePresentation.Observe(16f, now: 0f, presentationAvailable: true);
unavailablePresentation.Observe(34f, now: 1f, presentationAvailable: false);
WildernessDangerTransition unavailableArrival = unavailablePresentation.Observe(34f, now: 3f, presentationAvailable: false);
ExpectTrue(
    unavailableArrival.ArrivalBlock == WildernessDangerArrivalBlock.PresentationUnavailable,
    "hidden or missing HUD rejects presentation");

var respawnSuppression = new WildernessDangerTransitionTracker();
respawnSuppression.Observe(16f, now: 0f, presentationAvailable: true);
respawnSuppression.Observe(34f, now: 1f, presentationAvailable: true);
respawnSuppression.Observe(34f, now: 3f, presentationAvailable: true);
respawnSuppression.ResetForLifecycle();
WildernessDangerTransition respawnBaseline = respawnSuppression.Observe(34f, now: 10f, presentationAvailable: true);
ExpectTrue(respawnBaseline.BaselineEstablished, "respawn establishes a new baseline");
ExpectTrue(respawnBaseline.ArrivalDanger == null, "respawning in deadly does not present arrival noise");

var untunedEntry = new WildernessDangerTransitionTracker();
untunedEntry.Observe(16f, now: 0f, presentationAvailable: true);
untunedEntry.LeaveTunedWilderness();
WildernessDangerTransition tunedEntryCandidate = untunedEntry.Observe(34f, now: 5f, presentationAvailable: true);
ExpectTrue(tunedEntryCandidate.CandidateStarted, "return from untuned biome requires stable entry");
WildernessDangerTransition tunedEntryArrival = untunedEntry.Observe(34f, now: 7f, presentationAvailable: true);
ExpectTrue(tunedEntryArrival.ArrivalDanger == WildernessDanger.Deadly, "stable untuned-to-deadly entry presents");

ExpectExpandedLabelBounds(nativeAnchoredY: -20f, nativeHeight: 40f, pivotY: 0f, addedHeight: 30f);
ExpectExpandedLabelBounds(nativeAnchoredY: -20f, nativeHeight: 40f, pivotY: 0.5f, addedHeight: 30f);
ExpectExpandedLabelBounds(nativeAnchoredY: -20f, nativeHeight: 40f, pivotY: 1f, addedHeight: 30f);
ExpectExpandedLabelBounds(nativeAnchoredY: -20f, nativeHeight: 40f, pivotY: 0.5f, addedHeight: 0f);
ExpectNoAccumulatedLabelGrowth(nativeAnchoredY: -20f, nativeHeight: 40f, pivotY: 0f, addedHeight: 30f);
ExpectNoAccumulatedLabelGrowth(nativeAnchoredY: -20f, nativeHeight: 40f, pivotY: 0.5f, addedHeight: 30f);
ExpectNoAccumulatedLabelGrowth(nativeAnchoredY: -20f, nativeHeight: 40f, pivotY: 1f, addedHeight: 30f);
ExpectThrows<ArgumentOutOfRangeException>(
    () => WildernessMapLabelLayout.ExpandDownward(0f, 40f, 0.5f, -1f),
    "negative map-label growth is rejected");

ExpectTrue(WildernessStarChance.ShouldAdjust(eventSpawner: false, inInterior: false, hasBiomeTuning: true), "ordinary tuned wilderness adjusts");
ExpectTrue(!WildernessStarChance.ShouldAdjust(eventSpawner: true, inInterior: false, hasBiomeTuning: true), "random-event spawn is excluded");
ExpectTrue(!WildernessStarChance.ShouldAdjust(eventSpawner: false, inInterior: true, hasBiomeTuning: true), "dungeon-height spawn is excluded");
ExpectTrue(!WildernessStarChance.ShouldAdjust(eventSpawner: false, inInterior: false, hasBiomeTuning: false), "untuned biome preserves native chance");

ExpectTrue(BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.Meadows, out BiomeChanceCurve meadows), "Meadows tuning exists");
ExpectCurve(meadows, 10f, 12f, "Meadows starter tuning");
ExpectTrue(BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.BlackForest, out BiomeChanceCurve blackForest), "Black Forest tuning exists");
ExpectCurve(blackForest, 10f, 18f, "Black Forest starter tuning");
ExpectTrue(BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.Swamp, out BiomeChanceCurve swamp), "Swamp tuning exists");
ExpectCurve(swamp, 12f, 22f, "Swamp starter tuning");
ExpectTrue(BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.Mountain, out BiomeChanceCurve mountain), "Mountain tuning exists");
ExpectCurve(mountain, 14f, 24f, "Mountain starter tuning");
ExpectTrue(BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.Plains, out BiomeChanceCurve plains), "Plains tuning exists");
ExpectCurve(plains, 16f, 27f, "Plains starter tuning");
ExpectTrue(BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.Mistlands, out BiomeChanceCurve mistlands), "Mistlands tuning exists");
ExpectCurve(mistlands, 18f, 30f, "Mistlands starter tuning");
ExpectTrue(!BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.Ocean, out _), "Ocean stays native");
ExpectTrue(!BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.AshLands, out _), "Ashlands stays native");
ExpectTrue(!BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.DeepNorth, out _), "Deep North stays native");
ExpectTrue(!BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.None, out _), "non-biome input stays native");

ExpectClose(10f, WildernessStarChance.ComposeChance(meadows, 0f, WorldSize), "Meadows center chance");
ExpectClose(16f, WildernessStarChance.ComposeChance(meadows, 5_000f, WorldSize), "Meadows midpoint chance");
ExpectClose(22f, WildernessStarChance.ComposeChance(meadows, WorldSize, WorldSize), "Meadows world-edge chance");
ExpectClose(14.5f, WildernessStarChance.ComposeChance(blackForest, 2_500f, WorldSize), "Black Forest quarter-world chance");
ExpectClose(24f, WildernessStarChance.ComposeChance(swamp, 6_000f, WorldSize), "Swamp current-world distance chance");
ExpectClose(24f, WildernessStarChance.ComposeChance(mountain, 5_000f, WorldSize), "Mountain midpoint chance");
ExpectClose(32.8f, WildernessStarChance.ComposeChance(plains, 8_000f, WorldSize), "Plains remote chance");
ExpectClose(31.2f, WildernessStarChance.ComposeChance(mistlands, 6_000f, WorldSize), "Mistlands current-world distance chance");
ExpectClose(40f, WildernessStarChance.ComposeChance(mistlands, WorldSize, WorldSize), "Mistlands constructed maximum");

ExpectThrows<ArgumentOutOfRangeException>(
    () => WildernessStarChance.NormalizeDistance(0f, 0f),
    "invalid world size is rejected");
ExpectThrows<ArgumentOutOfRangeException>(
    () => new BiomeChanceCurve(10f, 9f),
    "descending biome chance curve is rejected");

Console.WriteLine("enemy tier distance and scope checks passed");
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

static void ExpectCurve(BiomeChanceCurve curve, float minimumChance, float maximumChance, string scenario)
{
    ExpectClose(minimumChance, curve.MinimumChance, $"{scenario} minimum chance");
    ExpectClose(maximumChance, curve.MaximumChance, $"{scenario} maximum chance");
}

static void ExpectExpandedLabelBounds(
    float nativeAnchoredY,
    float nativeHeight,
    float pivotY,
    float addedHeight)
{
    WildernessMapLabelBounds expanded = WildernessMapLabelLayout.ExpandDownward(
        nativeAnchoredY,
        nativeHeight,
        pivotY,
        addedHeight);
    float nativeTop = nativeAnchoredY + ((1f - pivotY) * nativeHeight);
    float expandedTop = expanded.AnchoredY + ((1f - pivotY) * expanded.SizeDeltaY);
    ExpectClose(nativeTop, expandedTop, $"map label pivot {pivotY}: top edge remains fixed");
    ExpectClose(
        nativeHeight + addedHeight,
        expanded.SizeDeltaY,
        $"map label pivot {pivotY}: bounds grow downward");
}

static void ExpectNoAccumulatedLabelGrowth(
    float nativeAnchoredY,
    float nativeHeight,
    float pivotY,
    float addedHeight)
{
    WildernessMapLabelBounds first = WildernessMapLabelLayout.ExpandDownward(
        nativeAnchoredY,
        nativeHeight,
        pivotY,
        addedHeight);
    WildernessMapLabelBounds afterRestore = WildernessMapLabelLayout.ExpandDownward(
        nativeAnchoredY,
        nativeHeight,
        pivotY,
        addedHeight);
    ExpectClose(first.AnchoredY, afterRestore.AnchoredY, $"map label pivot {pivotY}: repeated composition keeps anchored position");
    ExpectClose(first.SizeDeltaY, afterRestore.SizeDeltaY, $"map label pivot {pivotY}: repeated composition keeps height");
}

static void ExpectThrows<TException>(Action action, string scenario) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"{scenario}: expected {typeof(TException).Name}");
}

internal static class Heightmap
{
    [Flags]
    internal enum Biome
    {
        None = 0,
        Meadows = 1,
        Swamp = 2,
        Mountain = 4,
        BlackForest = 8,
        Plains = 0x10,
        AshLands = 0x20,
        DeepNorth = 0x40,
        Ocean = 0x100,
        Mistlands = 0x200,
        All = 0x37F,
    }
}
