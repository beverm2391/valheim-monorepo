using System.Collections.Generic;

namespace BenheimQoL;

internal static class Plugin
{
    internal static FakeLog Log { get; } = new FakeLog();
}

internal sealed class FakeLog
{
    internal List<string> Info { get; } = new List<string>();
    internal List<string> Warnings { get; } = new List<string>();

    public void LogInfo(string message) => Info.Add(message);
    public void LogWarning(string message) => Warnings.Add(message);
}
