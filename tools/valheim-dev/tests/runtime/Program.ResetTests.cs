using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ValheimDev;

internal static partial class Program
{
    private static void LabResetRecovery(string[] fixtures)
    {
        ResetRuntime();
        Authorize();
        Environment.SetEnvironmentVariable("VALHEIM_DEV_FAIL_CLEANUP_ONCE", "1");
        JsonElement failing = Install("failing-cleanup", "affinity.weapon-icon", fixtures[4]);
        Require(failing.GetProperty("ok").GetBoolean(), "change with a callable cleanup can install");
        JsonElement cleanupFailure = Remove("remove-failing-cleanup", "affinity.weapon-icon");
        Require(cleanupFailure.GetProperty("error").GetString() == "change_cleanup_failed"
            && cleanupFailure.GetProperty("cleanup_state").GetString() == "restart_required",
            "failed cleanup is explicit on the operation that attempted it");
        Require(Status().GetProperty("restart_required").GetBoolean(),
            "status exposes the sticky restart requirement");
        Require(Install("blocked-after-cleanup", "another-change", fixtures[0])
                .GetProperty("error").GetString() == "restart_required",
            "cleanup uncertainty blocks normal mutation until Lab reset or restart");
        Terminal dirtyStatus = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "status" }, dirtyStatus);
        Require(dirtyStatus.Lines[0].Contains("reset_lab", StringComparison.Ordinal)
            && dirtyStatus.Lines[0].Contains("affinity.weapon-icon", StringComparison.Ordinal),
            "authorized console status directs same-world recovery through Lab reset");
        Terminal dirtyOff = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, dirtyOff);
        Require(dirtyOff.Lines[0].Contains("reset_lab", StringComparison.Ordinal)
            && dirtyOff.Lines[0].Contains("affinity.weapon-icon", StringComparison.Ordinal),
            "off preserves sticky cleanup reporting and explains same-world recovery");
        Authorize();
        Require(Status().GetProperty("restart_required").GetBoolean(),
            "same-world recovery authorization preserves the cleanup lockout");
        JsonElement recoveredAfterOff = ResetLab("reset-after-off");
        Require(recoveredAfterOff.GetProperty("ok").GetBoolean()
            && !recoveredAfterOff.GetProperty("restart_required").GetBoolean()
            && recoveredAfterOff.GetProperty("active_changes").GetArrayLength() == 0
            && !ValheimDevTestSurface.Visible,
            "Lab reset remains reachable after access is closed and re-enabled in the same world");
        Require(Install("install-after-reset", "affinity.weapon-icon", fixtures[0]).GetProperty("ok").GetBoolean(),
            "successful Lab reset allows later mutation without a process restart");

        PartialLabReset(fixtures[4]);
        ReverseCleanupOrder(fixtures[0]);
    }

    private static void PartialLabReset(string failCleanupFixture)
    {
        ResetRuntime();
        Authorize();
        Environment.SetEnvironmentVariable("VALHEIM_DEV_FAIL_CLEANUP_ONCE", "1");
        Require(Install("partial-reset-first", "reset.partial.first", failCleanupFixture).GetProperty("ok").GetBoolean(),
            "partial reset proof installs the first fail-once cleanup change");
        Require(Install("partial-reset-second", "reset.partial.second", failCleanupFixture).GetProperty("ok").GetBoolean(),
            "partial reset proof installs the second fail-once cleanup change");
        Require(Remove("partial-reset-remove-first", "reset.partial.first")
                .GetProperty("cleanup_state").GetString() == "restart_required",
            "partial reset proof marks the first change uncertain");
        JsonElement partialReset = ResetLab("partial-reset");
        Require(!partialReset.GetProperty("ok").GetBoolean()
            && partialReset.GetProperty("restart_required").GetBoolean()
            && partialReset.GetProperty("active_changes").GetArrayLength() == 1
            && partialReset.GetProperty("active_changes")[0].GetProperty("change_id").GetString() == "reset.partial.second",
            "partial Lab reset removes recovered changes and preserves only the remaining failure");
        Terminal partialStatus = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "status" }, partialStatus);
        Require(partialStatus.Lines[0].Contains("reset.partial.second", StringComparison.Ordinal)
            && !partialStatus.Lines[0].Contains("reset.partial.first", StringComparison.Ordinal),
            "partial Lab reset removes recovered IDs from uncertainty reporting");
        Require(ResetLab("partial-reset-retry").GetProperty("ok").GetBoolean(),
            "a later Lab reset can clean the remaining fail-once change");
    }

    private static void ReverseCleanupOrder(string goodFixture)
    {
        ResetRuntime();
        Authorize();
        string cleanupMarker = Path.Combine(root, "cleanup-order.txt");
        Environment.SetEnvironmentVariable("VALHEIM_DEV_CLEANUP_MARKER", cleanupMarker);
        Environment.SetEnvironmentVariable("VALHEIM_DEV_CLEANUP_LABEL", "first");
        Require(Install("reset-order-first", "reset.order.first", goodFixture).GetProperty("ok").GetBoolean(),
            "reset order proof installs the first managed change");
        Environment.SetEnvironmentVariable("VALHEIM_DEV_CLEANUP_LABEL", "second");
        Require(Install("reset-order-second", "reset.order.second", goodFixture).GetProperty("ok").GetBoolean(),
            "reset order proof installs the second managed change");
        Require(ResetLab("reset-order").GetProperty("ok").GetBoolean()
            && string.Join(",", File.ReadAllLines(cleanupMarker)) == "second,first",
            "Lab reset cleans managed changes in reverse installation order");
    }

    private static JsonElement ResetLab(string operationId)
    {
        return Parse(Pump(SendAsync(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "reset_lab", ["protocol"] = ValheimDevProtocol.ProtocolVersion,
            ["session_id"] = sessionId, ["operation_id"] = operationId
        }))));
    }
}
