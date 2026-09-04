using System;
using System.IO;

public static class ValheimDevExperiment
{
    public static string Run()
    {
        ValheimDevTestEvidence.EmitSynchronousEvent();
        return "good-result";
    }
    public static void Cleanup()
    {
        string? marker = Environment.GetEnvironmentVariable("VALHEIM_DEV_CLEANUP_MARKER");
        if (!string.IsNullOrEmpty(marker)) File.AppendAllText(marker, "cleaned\n");
    }
}
