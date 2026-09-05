using System;

public static class ValheimDevChange
{
    private static int cleanupCount;

    public static string Run()
    {
        if (Environment.GetEnvironmentVariable("VALHEIM_DEV_FAIL_RUN") == "1")
        {
            throw new InvalidOperationException("candidate exploded");
        }
        return "fail-cleanup-result";
    }
    public static void Cleanup()
    {
        if (Environment.GetEnvironmentVariable("VALHEIM_DEV_FAIL_CLEANUP_ONCE") == "1")
        {
            if (cleanupCount++ == 0) throw new InvalidOperationException("cleanup exploded once");
            return;
        }
        throw new InvalidOperationException("cleanup exploded");
    }
}
