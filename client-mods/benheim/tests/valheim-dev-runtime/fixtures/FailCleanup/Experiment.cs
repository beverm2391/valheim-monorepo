using System;

public static class ValheimDevExperiment
{
    public static string Run() => "fail-cleanup-result";
    public static void Cleanup() => throw new InvalidOperationException("cleanup exploded");
}
