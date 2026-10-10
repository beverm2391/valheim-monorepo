namespace BenheimServerSupport;

internal enum ResidentPlacementProgress
{
    WaitingForOwner,
    WaitingForReplica,
    TimedOut,
    WaitingAfterTimeout,
    Applied,
    LateApplied,
    Rejected,
    Expired
}

/// <summary>
/// Tracks the two independent signals needed to confirm a tub placement:
/// the authenticated owner acknowledgement and the replicated generation/flag.
/// The owner deadline and bounded late window use ZNet.GetTimeSeconds on both
/// ends, so a routed request cannot mutate the tub after its result timed out.
/// </summary>
internal sealed class ResidentPlacementConfirmation
{
    private readonly int expectedGeneration;
    private readonly bool desired;
    private readonly double deadline;
    private readonly double lateWatchSeconds;
    private bool ownerResultReceived;
    private bool ownerSucceeded;
    private int ownerGeneration;
    private string ownerReason = "owner_rejected";
    private bool timeoutReported;
    private double lateDeadline;

    internal ResidentPlacementConfirmation(
        int expectedGeneration,
        bool desired,
        double deadline,
        double lateWatchSeconds)
    {
        this.expectedGeneration = expectedGeneration;
        this.desired = desired;
        this.deadline = deadline;
        this.lateWatchSeconds = lateWatchSeconds;
    }

    internal bool TimeoutReported => timeoutReported;

    internal double LateDeadline => lateDeadline;

    internal bool TryRecordOwnerResult(bool success, string reason, int generation)
    {
        if (ownerResultReceived)
        {
            return false;
        }

        ownerResultReceived = true;
        ownerSucceeded = success;
        ownerReason = string.IsNullOrWhiteSpace(reason) ? "owner_rejected" : reason;
        ownerGeneration = generation;
        return true;
    }

    internal ResidentPlacementProgress Observe(
        double now,
        bool hasReplicatedState,
        int replicatedGeneration,
        bool invited,
        out string reason)
    {
        reason = string.Empty;
        if (ownerResultReceived)
        {
            if (!ownerSucceeded)
            {
                reason = ownerReason;
                return ResidentPlacementProgress.Rejected;
            }

            if (ownerGeneration != expectedGeneration + 1)
            {
                reason = "owner_generation_mismatch";
                return ResidentPlacementProgress.Rejected;
            }

            if (hasReplicatedState)
            {
                if (replicatedGeneration == ownerGeneration)
                {
                    if (invited == desired)
                    {
                        return timeoutReported
                            ? ResidentPlacementProgress.LateApplied
                            : ResidentPlacementProgress.Applied;
                    }

                    reason = "owner_state_mismatch";
                    return ResidentPlacementProgress.Rejected;
                }

                if (replicatedGeneration > ownerGeneration)
                {
                    reason = "owner_state_superseded";
                    return ResidentPlacementProgress.Rejected;
                }
            }
        }

        if (!timeoutReported && now >= deadline)
        {
            timeoutReported = true;
            lateDeadline = now + lateWatchSeconds;
            reason = "placement_timeout_unknown";
            return ResidentPlacementProgress.TimedOut;
        }

        if (timeoutReported && now >= lateDeadline)
        {
            reason = "late_confirmation_expired";
            return ResidentPlacementProgress.Expired;
        }

        return ownerResultReceived
            ? ResidentPlacementProgress.WaitingForReplica
            : timeoutReported
                ? ResidentPlacementProgress.WaitingAfterTimeout
                : ResidentPlacementProgress.WaitingForOwner;
    }
}
