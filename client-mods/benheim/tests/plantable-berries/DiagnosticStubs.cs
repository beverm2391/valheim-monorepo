using System;
using System.Collections.Generic;

namespace BenheimQoL.Infrastructure
{
    internal static class Diagnostics
    {
        internal static readonly List<string> Events = new();
        internal static readonly List<string> TypedJson = new();
        internal static bool FailEmission;
        internal static string Flatten(string value) => value;
        internal static void Event(string domain, string name, string fields) => Events.Add($"{domain}/{name}");
        internal static void Emit(DiagnosticEvent value)
        {
            if (FailEmission) throw new InvalidOperationException("test sink unavailable");
            value.Prepare(DateTime.UtcNow, "berry-test-session", "test");
            Events.Add($"{value.Domain}/{value.Name}");
            TypedJson.Add(value.ToJsonLine());
        }
    }
}

namespace BenheimQoL
{
    internal static class Plugin
    {
        internal static readonly TestLog Log = new();
    }

    internal sealed class TestLog
    {
        internal readonly List<string> Errors = new();
        internal readonly List<string> Warnings = new();
        internal bool FailWarning;
        internal bool FailError;
        internal void LogError(object value)
        {
            Errors.Add(value.ToString() ?? "");
            if (FailError) throw new InvalidOperationException("test error logger unavailable");
        }
        internal void LogWarning(object value)
        {
            Warnings.Add(value.ToString() ?? "");
            if (FailWarning) throw new InvalidOperationException("test warning logger unavailable");
        }
    }
}
