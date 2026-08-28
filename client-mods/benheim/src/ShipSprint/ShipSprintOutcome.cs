namespace BenheimQoL.ShipSprint;

internal sealed class ShipSprintOutcome
{
    internal ShipSprintOutcome(
        string operationId,
        string shipType,
        string startingThrottle,
        string reason,
        float duration,
        float startingSpeed,
        float peakSpeed)
    {
        OperationId = operationId;
        ShipType = shipType;
        StartingThrottle = startingThrottle;
        Reason = reason;
        Duration = duration;
        StartingSpeed = startingSpeed;
        PeakSpeed = peakSpeed;
    }

    internal string OperationId { get; }
    internal string ShipType { get; }
    internal string StartingThrottle { get; }
    internal string Reason { get; }
    internal float Duration { get; }
    internal float StartingSpeed { get; }
    internal float PeakSpeed { get; }
}
