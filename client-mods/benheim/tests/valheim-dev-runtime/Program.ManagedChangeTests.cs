using System;
using System.Text.Json;
using BenheimQoL.ValheimDev;

internal static partial class Program
{
    private static void StaleChangeState(string changeAssembly)
    {
        int cleanupBeforeStale = ValheimDevTestSurface.CleanupCount;
        JsonElement staleInstall = Parse(Pump(SendAsync(CodeRequest(
            "install_change", "stale-install", "affinity.weapon-icon", changeAssembly,
            Array.Empty<string>(), 0, "install-a"))));
        Require(staleInstall.GetProperty("error").GetString() == "stale_change_state"
            && ValheimDevTestSurface.CleanupCount == cleanupBeforeStale
            && ValheimDevTestSurface.Variant == "pulse-b",
            "stale install refuses without cleaning or replacing the current version");
        JsonElement staleRemove = Remove("stale-remove", "affinity.weapon-icon", "install-a");
        Require(staleRemove.GetProperty("error").GetString() == "stale_change_state"
            && ValheimDevTestSurface.CleanupCount == cleanupBeforeStale
            && Status().GetProperty("active_changes")[0].GetProperty("operation_id").GetString() == "replace-b",
            "stale removal refuses without cleaning the current version");
    }
}
