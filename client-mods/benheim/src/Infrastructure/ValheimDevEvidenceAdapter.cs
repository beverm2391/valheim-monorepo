using System;

namespace BenheimQoL.Infrastructure;

// This deliberately tiny reflection boundary makes Benheim an optional
// evidence provider. Valheim Dev never references the Benheim assembly, and
// Benheim never references the Valheim Dev assembly.
internal static class ValheimDevEvidenceAdapter
{
    internal static string SetExternalObserver(
        string owner,
        Action<string, string, string>? observer)
    {
        if (!string.Equals(owner, "ValheimDev", StringComparison.Ordinal))
        {
            return "unsupported_consumer";
        }
        if (observer != null && !HealthReporting.GameplayActionsEnabled)
        {
            return "optional_provider_unhealthy";
        }

        Diagnostics.SetExternalObserver(observer);
        return "available";
    }
}
