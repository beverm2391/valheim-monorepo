using System;
using System.IO;

namespace BepInEx;

internal static class Paths
{
    internal static string ConfigPath { get; } = Path.Combine(
        Path.GetTempPath(), "benheim-speech-trace-test-" + Guid.NewGuid().ToString("N"));
}
