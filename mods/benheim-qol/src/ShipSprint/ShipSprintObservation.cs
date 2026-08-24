using System;

namespace BenheimQoL.ShipSprint;

// One observation follows one continuous owner-authoritative boost segment.
// An ownership transfer closes the old segment and starts another on the new
// owner, which keeps diagnostics bounded without persisting telemetry state.
internal sealed class ShipSprintObservation
{
    private bool active;
    private string operationId = string.Empty;
    private string shipType = string.Empty;
    private string startingThrottle = string.Empty;
    private float startedAt;
    private float startingSpeed;
    private float peakSpeed;

    internal ShipSprintOutcome? Observe(
        bool shouldBoost,
        float now,
        float speed,
        string currentShipType,
        string currentThrottle,
        string stopReason,
        Func<string> newOperationId)
    {
        if (shouldBoost)
        {
            if (!active)
            {
                active = true;
                operationId = newOperationId();
                shipType = currentShipType;
                startingThrottle = currentThrottle;
                startedAt = now;
                startingSpeed = speed;
                peakSpeed = speed;
            }
            else
            {
                peakSpeed = Math.Max(peakSpeed, speed);
            }

            return null;
        }

        return Finish(now, speed, stopReason);
    }

    internal void RecordPeak(float speed)
    {
        if (active)
        {
            peakSpeed = Math.Max(peakSpeed, speed);
        }
    }

    internal ShipSprintOutcome? Finish(float now, float speed, string reason)
    {
        if (!active)
        {
            return null;
        }

        peakSpeed = Math.Max(peakSpeed, speed);
        ShipSprintOutcome outcome = new ShipSprintOutcome(
            operationId,
            shipType,
            startingThrottle,
            reason,
            Math.Max(0f, now - startedAt),
            startingSpeed,
            peakSpeed);
        active = false;
        operationId = string.Empty;
        return outcome;
    }
}
