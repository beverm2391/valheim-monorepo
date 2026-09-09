using System;
using System.Collections.Generic;

namespace BenheimQoL.Infrastructure;

internal static class Diagnostics
{
    internal static List<DiagnosticEvent> Events = new();
    internal static bool ThrowOnEmit;

    internal static void Emit(DiagnosticEvent record)
    {
        if (ThrowOnEmit) throw new InvalidOperationException("diagnostic sink failed");
        record.Prepare(DateTime.UtcNow, "world-label-test", "test");
        Events.Add(record);
    }
}
