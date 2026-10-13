using System.Collections.Generic;
using BenheimQoL.Infrastructure;

namespace BenheimServerSupport;

internal static class ServerDiagnostics
{
    internal static readonly List<DiagnosticEvent> Events = new();
    internal static void Emit(DiagnosticEvent diagnostic) => Events.Add(diagnostic);
}
