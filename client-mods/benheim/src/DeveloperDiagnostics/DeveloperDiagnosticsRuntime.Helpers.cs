using System;
using System.Collections.Generic;

namespace BenheimQoL.DeveloperDiagnostics;

internal static partial class DeveloperDiagnosticsRuntime
{
    private static bool TryParseOverride(
        string value,
        out ProbeSessionOverride requestedOverride)
    {
        if (string.Equals(value, "default", StringComparison.OrdinalIgnoreCase))
        {
            requestedOverride = ProbeSessionOverride.Default;
            return true;
        }
        if (string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
        {
            requestedOverride = ProbeSessionOverride.On;
            return true;
        }
        if (string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
        {
            requestedOverride = ProbeSessionOverride.Off;
            return true;
        }

        requestedOverride = default;
        return false;
    }

    private static List<string> CatalogNames() => SortedNames(Catalogs.Keys);

    private static List<string> SnapshotNames() => SortedNames(Snapshots.Keys);

    private static List<string> ProbeNames() => SortedNames(Probes.Keys);

    private static List<string> SortedNames(IEnumerable<string> names)
    {
        List<string> sorted = new(names);
        sorted.Sort(StringComparer.OrdinalIgnoreCase);
        return sorted;
    }

    private static string[] Tail(string[] arguments, int start)
    {
        string[] tail = new string[Math.Max(0, arguments.Length - start)];
        Array.Copy(arguments, start, tail, 0, tail.Length);
        return tail;
    }

    private static string StateName(bool state) => state ? "on" : "off";

    private static string Flatten(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "unknown failure"
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
