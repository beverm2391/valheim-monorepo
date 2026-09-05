using System;
using System.IO;

public static class ValheimDevChange
{
    private static readonly string Variant =
        Environment.GetEnvironmentVariable("VALHEIM_DEV_VARIANT") ?? "default";
    private static int runCount;

    public static string Run()
    {
        if (Environment.GetEnvironmentVariable("VALHEIM_DEV_FAIL_ON_RESTORE") == "1" && runCount++ > 0)
        {
            throw new InvalidOperationException("restore exploded");
        }
        ValheimDevTestSurface.Visible = true;
        ValheimDevTestSurface.Variant = Variant;
        ValheimDevTestEvidence.EmitSynchronousEvent();
        return ValheimDevTestSurface.Describe();
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

public static class ValheimDevInspection
{
    public static string Run() => ValheimDevTestSurface.Describe();
}
