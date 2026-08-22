using System;

namespace BenheimQoL.WeaponRhythm;

/// <summary>
/// Keeps optional presentation and telemetry from suppressing Valheim's native
/// target-owner damage call. Each optional step fails independently. Native
/// damage still runs once even when failure reporting also fails.
/// </summary>
internal static class PerfectImpactOutcomeDelivery
{
    internal static void Deliver(
        Action? present,
        Action emitDiagnostic,
        Action nativeDamage,
        Action<Exception> reportFailure)
    {
        if (present != null)
        {
            RunOptional(present, reportFailure);
        }

        RunOptional(emitDiagnostic, reportFailure);
        nativeDamage();
    }

    private static void RunOptional(Action action, Action<Exception> reportFailure)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            try
            {
                reportFailure(exception);
            }
            catch
            {
                // Failure reporting is optional too. Native damage remains the
                // mandatory step and must still run.
            }
        }
    }
}
