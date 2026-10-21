// The normal fixture exercises one-shot and managed standalone bridge paths.
using System;
using System.IO;
using System.Text.Json;
using ValheimDev;

public static class ValheimDevChange
{
    private static readonly string Variant =
        Environment.GetEnvironmentVariable("VALHEIM_DEV_VARIANT") ?? "default";
    private static readonly string CleanupLabel =
        Environment.GetEnvironmentVariable("VALHEIM_DEV_CLEANUP_LABEL") ?? "cleaned";
    private static int runCount;

    internal static string HelperResult() => "{\"variant\":\"" + Variant
        + "\",\"instance\":\"" + FixtureHelper.Instance + "\"}";

    public static string Run(string inputJson)
    {
        if (Environment.GetEnvironmentVariable("VALHEIM_DEV_FAIL_ON_RESTORE") == "1" && runCount++ > 0)
        {
            throw new InvalidOperationException("restore exploded");
        }
        ValheimDevTestSurface.Visible = true;
        ValheimDevTestSurface.Variant = Variant;
        ValheimDevTestEvidence.EmitSynchronousEvent();
        return "{\"snapshot\":" + ValheimDevTestSurface.Describe()
            + ",\"helper\":" + FixtureHelper.Read() + ",\"input\":" + inputJson + "}";
    }
    public static void Cleanup()
    {
        ValheimDevTestSurface.Visible = false;
        ValheimDevTestSurface.Variant = "baseline";
        ValheimDevTestSurface.CleanupCount++;
        string? marker = Environment.GetEnvironmentVariable("VALHEIM_DEV_CLEANUP_MARKER");
        if (!string.IsNullOrEmpty(marker)) File.AppendAllText(marker, CleanupLabel + "\n");
    }
}

public static class ValheimDevCommand
{
    public static string Run(string inputJson)
    {
        using JsonDocument input = JsonDocument.Parse(inputJson);
        if (input.RootElement.ValueKind == JsonValueKind.Object
            && input.RootElement.TryGetProperty("helper_operation_id", out JsonElement operationId))
        {
            Type helper = ValheimDevManagedChanges.ResolveType(
                input.RootElement.GetProperty("change_id").GetString()!, operationId.GetString()!,
                input.RootElement.GetProperty("type_name").GetString()!);
            return (string)helper.GetMethod("Read")!.Invoke(null, null)!;
        }
        string? variant = Environment.GetEnvironmentVariable("VALHEIM_DEV_COMMAND_VARIANT");
        if (!string.IsNullOrEmpty(variant))
        {
            ValheimDevTestSurface.Visible = true;
            ValheimDevTestSurface.Variant = variant;
        }
        return "{\"snapshot\":" + ValheimDevTestSurface.Describe() + ",\"input\":" + inputJson + "}";
    }
}

public static class FixtureHelper
{
    internal static readonly string Instance = Guid.NewGuid().ToString("N");
    public static string Read() => ValheimDevChange.HelperResult();
}
