using System;

public static class ValheimDevChange
{
    public static string Run(string inputJson)
    {
        ValheimDevTestSurface.Visible = true;
        ValheimDevTestSurface.Variant = "broken";
        throw new InvalidOperationException("change exploded");
    }

    public static void Cleanup()
    {
        ValheimDevTestSurface.Visible = false;
        ValheimDevTestSurface.Variant = "baseline";
        ValheimDevTestSurface.CleanupCount++;
    }
}
