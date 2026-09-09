using System;

namespace BenheimQoL.DeveloperDiagnostics;

internal enum ProbeSessionOverride
{
    Default,
    On,
    Off,
}

internal enum DiagnosticProbeKind
{
    Event,
    Visual,
}

internal enum DiagnosticProbeCleanupReason
{
    Disabled,
    WorldExit,
    SessionReset,
    Failure,
}

internal delegate bool DiagnosticProbeActivation(bool active, out string failure);

internal static partial class DeveloperDiagnosticsRuntime
{
    private sealed class RegisteredProbe
    {
        internal RegisteredProbe(
            string name,
            DiagnosticProbeKind kind,
            bool shippedDefault,
            DiagnosticProbeActivation setActive,
            Action update,
            Action<DiagnosticProbeCleanupReason> cleanup)
        {
            Name = name;
            Kind = kind;
            ShippedDefault = shippedDefault;
            SetActive = setActive;
            Update = update;
            Cleanup = cleanup;
        }

        internal string Name { get; }
        internal DiagnosticProbeKind Kind { get; }
        internal bool ShippedDefault { get; }
        internal DiagnosticProbeActivation SetActive { get; }
        internal Action Update { get; }
        internal Action<DiagnosticProbeCleanupReason> Cleanup { get; }
        internal ProbeSessionOverride Override { get; set; }
        internal bool Active { get; set; }
    }
}
