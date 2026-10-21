using System;
using System.Text.Json;
using ValheimDev;

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

    private static JsonElement ReadManagedHelper(string assembly, string operationId, string typeName = "FixtureHelper")
        => RunOnce("read-helper-" + operationId, assembly, JsonSerializer.Serialize(new
        {
            change_id = "affinity.weapon-icon", helper_operation_id = operationId, type_name = typeName
        }));

    private static void HelperResolutionRequiresCommand()
    {
        bool refused = false;
        try { ValheimDevManagedChanges.ResolveType("affinity.weapon-icon", "replace-b", "FixtureHelper"); }
        catch (InvalidOperationException error)
        {
            refused = error.Message.Contains("inside a Lab command", StringComparison.Ordinal);
        }
        Require(refused, "public helper resolution refuses calls outside a run_once command");
    }

    private static void RequireHelperFailure(JsonElement response, string expected)
    {
        Require(!response.GetProperty("ok").GetBoolean()
            && response.GetProperty("exception").GetString()!.Contains(expected, StringComparison.Ordinal),
            "helper lookup refuses " + expected);
    }
}
