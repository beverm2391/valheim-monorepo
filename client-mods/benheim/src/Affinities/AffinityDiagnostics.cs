using System;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.Affinities;

internal static class AffinityDiagnostics
{
    internal static void Emit(DiagnosticEvent diagnosticEvent)
    {
        try
        {
            Diagnostics.Emit(diagnosticEvent);
        }
        catch (Exception exception)
        {
            // Diagnostics are evidence, not part of the item/resource transaction.
            // A broken optional destination must never change gameplay state.
            try
            {
                Plugin.Log.LogWarning(
                    $"Affinity diagnostics failed: {Diagnostics.Flatten(exception.Message)}");
            }
            catch
            {
                // Logging is best-effort at this boundary too.
            }
        }
    }
}
