using BenheimQoL.Infrastructure;

namespace BenheimQoL.ShipSprint;

internal static class ShipSprintDiagnostics
{
    internal static DiagnosticEvent CreateEvent(ShipSprintOutcome outcome)
    {
        return DiagnosticEvent.Create("ShipSprint", "ship_sprint_finished")
            .String("operation_id", outcome.OperationId)
            .String("operation_phase", "terminal")
            .String("ship_type", outcome.ShipType)
            .String("starting_throttle", outcome.StartingThrottle)
            .String("reason", outcome.Reason)
            .Number("duration", outcome.Duration)
            .Number("starting_speed", outcome.StartingSpeed)
            .Number("peak_speed", outcome.PeakSpeed)
            .Number("thrust_multiplier", ShipSprintTuning.ThrustMultiplier);
    }
}
