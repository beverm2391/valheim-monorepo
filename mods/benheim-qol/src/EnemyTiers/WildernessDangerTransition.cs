namespace BenheimQoL.EnemyTiers;

internal enum WildernessDangerArrivalBlock
{
    None,
    Cooldown,
    PresentationUnavailable,
}

internal readonly struct WildernessDangerTransition
{
    internal WildernessDangerTransition(
        bool baselineEstablished = false,
        bool candidateStarted = false,
        bool candidateCancelled = false,
        bool stableChanged = false,
        WildernessDanger previousDanger = WildernessDanger.Safe,
        WildernessDanger currentDanger = WildernessDanger.Safe,
        WildernessDanger? arrivalDanger = null,
        WildernessDangerArrivalBlock arrivalBlock = WildernessDangerArrivalBlock.None,
        float cooldownRemaining = 0f)
    {
        BaselineEstablished = baselineEstablished;
        CandidateStarted = candidateStarted;
        CandidateCancelled = candidateCancelled;
        StableChanged = stableChanged;
        PreviousDanger = previousDanger;
        CurrentDanger = currentDanger;
        ArrivalDanger = arrivalDanger;
        ArrivalBlock = arrivalBlock;
        CooldownRemaining = cooldownRemaining;
    }

    internal bool BaselineEstablished { get; }
    internal bool CandidateStarted { get; }
    internal bool CandidateCancelled { get; }
    internal bool StableChanged { get; }
    internal WildernessDanger PreviousDanger { get; }
    internal WildernessDanger CurrentDanger { get; }
    internal WildernessDanger? ArrivalDanger { get; }
    internal WildernessDangerArrivalBlock ArrivalBlock { get; }
    internal float CooldownRemaining { get; }
}

/// <summary>
/// Converts the continuous wilderness chance into a stable local presentation
/// state. The map hover remains exact; only the current-player indicator and
/// arrival cue use this debounce and hysteresis layer.
/// </summary>
internal sealed class WildernessDangerTransitionTracker
{
    internal const float DebounceSeconds = 2f;
    internal const float HysteresisPercent = 0.75f;
    internal const float ArrivalCooldownSeconds = 60f;

    private bool hasStableDanger;
    private WildernessDanger stableDanger;
    private bool hasCandidate;
    private WildernessDanger candidateDanger;
    private float candidateSince;
    private float nextArrivalAt;
    private bool suppressNextBaseline = true;

    internal bool HasStableDanger => hasStableDanger;
    internal WildernessDanger StableDanger => stableDanger;

    internal WildernessDangerTransition Observe(
        float perStepChance,
        float now,
        bool presentationAvailable)
    {
        WildernessDanger observed = hasStableDanger
            ? ClassifyWithHysteresis(perStepChance, stableDanger)
            : WildernessDangerScale.Classify(perStepChance);

        if (!hasStableDanger)
        {
            if (suppressNextBaseline)
            {
                stableDanger = observed;
                hasStableDanger = true;
                hasCandidate = false;
                suppressNextBaseline = false;
                return new WildernessDangerTransition(
                    baselineEstablished: true,
                    currentDanger: stableDanger);
            }

            return ObserveCandidateFromUnclassified(observed, now, presentationAvailable);
        }

        if (observed == stableDanger)
        {
            if (!hasCandidate)
            {
                return new WildernessDangerTransition(currentDanger: stableDanger);
            }

            hasCandidate = false;
            return new WildernessDangerTransition(
                candidateCancelled: true,
                currentDanger: stableDanger);
        }

        if (!hasCandidate || candidateDanger != observed)
        {
            candidateDanger = observed;
            candidateSince = now;
            hasCandidate = true;
            return new WildernessDangerTransition(
                candidateStarted: true,
                previousDanger: stableDanger,
                currentDanger: observed);
        }

        if (now - candidateSince < DebounceSeconds)
        {
            return new WildernessDangerTransition(
                previousDanger: stableDanger,
                currentDanger: candidateDanger);
        }

        WildernessDanger previous = stableDanger;
        stableDanger = candidateDanger;
        hasCandidate = false;
        return CompleteStableChange(previous, stableDanger, now, presentationAvailable);
    }

    /// <summary>
    /// An unlisted biome has no Benheim category. Returning to a listed biome is
    /// a real entry, so it must stabilize instead of becoming a silent login
    /// baseline.
    /// </summary>
    internal void LeaveTunedWilderness()
    {
        hasStableDanger = false;
        hasCandidate = false;
        suppressNextBaseline = false;
    }

    /// <summary>
    /// Login, character replacement, and respawn establish a silent baseline.
    /// The cooldown survives so those lifecycle changes cannot bypass it.
    /// </summary>
    internal void ResetForLifecycle()
    {
        hasStableDanger = false;
        hasCandidate = false;
        suppressNextBaseline = true;
    }

    /// <summary>
    /// A paused gameplay state keeps the last stable category but discards any
    /// unfinished transition. Time spent loading or in a cutscene must not
    /// satisfy the arrival debounce.
    /// </summary>
    internal void PauseObservation()
    {
        hasCandidate = false;
    }

    private WildernessDangerTransition ObserveCandidateFromUnclassified(
        WildernessDanger observed,
        float now,
        bool presentationAvailable)
    {
        if (!hasCandidate || candidateDanger != observed)
        {
            candidateDanger = observed;
            candidateSince = now;
            hasCandidate = true;
            return new WildernessDangerTransition(
                candidateStarted: true,
                currentDanger: observed);
        }

        if (now - candidateSince < DebounceSeconds)
        {
            return new WildernessDangerTransition(currentDanger: candidateDanger);
        }

        stableDanger = candidateDanger;
        hasStableDanger = true;
        hasCandidate = false;
        return CompleteStableChange(
            WildernessDanger.Safe,
            stableDanger,
            now,
            presentationAvailable);
    }

    private WildernessDangerTransition CompleteStableChange(
        WildernessDanger previous,
        WildernessDanger current,
        float now,
        bool presentationAvailable)
    {
        if (current < WildernessDanger.Dangerous || current <= previous)
        {
            return new WildernessDangerTransition(
                stableChanged: true,
                previousDanger: previous,
                currentDanger: current);
        }

        if (!presentationAvailable)
        {
            return new WildernessDangerTransition(
                stableChanged: true,
                previousDanger: previous,
                currentDanger: current,
                arrivalBlock: WildernessDangerArrivalBlock.PresentationUnavailable);
        }

        if (now < nextArrivalAt)
        {
            return new WildernessDangerTransition(
                stableChanged: true,
                previousDanger: previous,
                currentDanger: current,
                arrivalBlock: WildernessDangerArrivalBlock.Cooldown,
                cooldownRemaining: nextArrivalAt - now);
        }

        nextArrivalAt = now + ArrivalCooldownSeconds;
        return new WildernessDangerTransition(
            stableChanged: true,
            previousDanger: previous,
            currentDanger: current,
            arrivalDanger: current);
    }

    private static WildernessDanger ClassifyWithHysteresis(
        float perStepChance,
        WildernessDanger current)
    {
        WildernessDanger raw = WildernessDangerScale.Classify(perStepChance);
        if (raw == current)
        {
            return current;
        }

        if (raw > current)
        {
            float threshold = raw switch
            {
                WildernessDanger.Sketchy => WildernessDangerScale.SketchyThreshold,
                WildernessDanger.Dangerous => WildernessDangerScale.DangerousThreshold,
                WildernessDanger.Deadly => WildernessDangerScale.DeadlyThreshold,
                _ => float.PositiveInfinity,
            };
            return perStepChance >= threshold + HysteresisPercent ? raw : current;
        }

        float lowerThreshold = current switch
        {
            WildernessDanger.Sketchy => WildernessDangerScale.SketchyThreshold,
            WildernessDanger.Dangerous => WildernessDangerScale.DangerousThreshold,
            WildernessDanger.Deadly => WildernessDangerScale.DeadlyThreshold,
            _ => float.NegativeInfinity,
        };
        return perStepChance < lowerThreshold - HysteresisPercent ? raw : current;
    }
}
