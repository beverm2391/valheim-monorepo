using System;
using System.IO;

public static class ValheimDevChange
{
    private static readonly string Variant =
        Environment.GetEnvironmentVariable("VALHEIM_DEV_VARIANT") ?? "default";
    private static int runCount;

    public static string Run(string inputJson)
    {
        if (Environment.GetEnvironmentVariable("VALHEIM_DEV_FAIL_ON_RESTORE") == "1" && runCount++ > 0)
        {
            throw new InvalidOperationException("restore exploded");
        }
        ValheimDevTestSurface.Visible = true;
        ValheimDevTestSurface.Variant = Variant;
        ValheimDevTestEvidence.EmitSynchronousEvent();
        return "{\"snapshot\":" + ValheimDevTestSurface.Describe() + ",\"input\":" + inputJson + "}";
    }
    public static void Cleanup()
    {
        ValheimDevTestSurface.Visible = false;
        ValheimDevTestSurface.Variant = "baseline";
        ValheimDevTestSurface.CleanupCount++;
        string? marker = Environment.GetEnvironmentVariable("VALHEIM_DEV_CLEANUP_MARKER");
        if (!string.IsNullOrEmpty(marker)) File.AppendAllText(marker, "cleaned\n");
    }
}

public static class ValheimDevCommand
{
    public static string Run(string inputJson)
    {
        string? variant = Environment.GetEnvironmentVariable("VALHEIM_DEV_COMMAND_VARIANT");
        if (!string.IsNullOrEmpty(variant))
        {
            ValheimDevTestSurface.Visible = true;
            ValheimDevTestSurface.Variant = variant;
        }
        return "{\"snapshot\":" + ValheimDevTestSurface.Describe() + ",\"input\":" + inputJson + "}";
    }
}
