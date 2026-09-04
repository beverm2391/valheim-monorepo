using System;

public static class ValheimDevExperiment
{
    public static string Run() => throw new InvalidOperationException("experiment exploded");
    public static void Cleanup() { }
}
