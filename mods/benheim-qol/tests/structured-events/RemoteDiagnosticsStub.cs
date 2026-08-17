using System.Collections.Generic;

namespace BenheimQoL.Infrastructure;

internal static class RemoteDiagnostics
{
    internal static List<DiagnosticEvent> Enqueued { get; } = new List<DiagnosticEvent>();

    internal static void TryEnqueue(DiagnosticEvent diagnosticEvent)
    {
        Enqueued.Add(diagnosticEvent);
    }

    internal static void Reset() => Enqueued.Clear();
}
