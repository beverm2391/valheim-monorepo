namespace BenheimQoL.Infrastructure;

internal static class RemoteDiagnostics
{
    internal static bool? LastSharingValue { get; private set; }

    internal static void SetSharingEnabled(bool enabled)
    {
        LastSharingValue = enabled;
    }
}
