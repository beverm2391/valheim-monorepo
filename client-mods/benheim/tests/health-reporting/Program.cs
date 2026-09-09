using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;

namespace BepInEx.Logging
{
    internal sealed class ManualLogSource
    {
        internal readonly List<string> Errors = new();
        internal readonly List<string> Warnings = new();
        internal readonly List<string> Infos = new();

        internal void LogError(string message) => Errors.Add(message);
        internal void LogWarning(string message) => Warnings.Add(message);
        internal void LogInfo(string message) => Infos.Add(message);
    }
}

namespace BenheimQoL
{
    using BepInEx.Logging;

    internal static class Plugin
    {
        internal static readonly ManualLogSource Log = new();
    }
}

namespace BenheimQoL.Infrastructure
{
    internal sealed class DiagnosticEvent
    {
        internal static DiagnosticEvent Create(string domain, string name) => new(domain, name);

        private DiagnosticEvent(string domain, string name)
        {
            Domain = domain;
            Name = name;
        }

        internal string Domain { get; }
        internal string Name { get; }
        internal readonly Dictionary<string, string?> Fields = new();

        internal DiagnosticEvent String(string name, string? value)
        {
            Fields.Add(name, value);
            return this;
        }
    }

    internal static class Diagnostics
    {
        internal static readonly List<string> Events = new();
        internal static readonly List<DiagnosticEvent> TypedEvents = new();

        internal static void Event(string feature, string action, string details = "")
        {
            Events.Add($"[diag][{feature}] {action} {details}".TrimEnd());
        }

        internal static void Emit(DiagnosticEvent diagnosticEvent)
        {
            TypedEvents.Add(diagnosticEvent);
        }

        internal static string Flatten(string value)
        {
            return value.Replace('\r', ' ').Replace('\n', ' ').Replace(' ', '_');
        }
    }
}

public static class MessageHud
{
    public enum MessageType
    {
        Center,
    }
}

public sealed class Player
{
    public static Player? m_localPlayer;
    public int MessageCount { get; private set; }
    public bool ThrowNextMessage { get; set; }

    public void Message(MessageHud.MessageType type, string message)
    {
        if (ThrowNextMessage)
        {
            ThrowNextMessage = false;
            throw new InvalidOperationException("message hud not ready");
        }

        MessageCount++;
    }
}

namespace HealthReportingTests
{
    internal static class Program
    {
        private static int Main()
        {
            HealthReporting.BeginSession();
            Require(HealthReporting.GameplayActionsEnabled, "new sessions start enabled");

            HealthReporting.ReportKeybindInspectionFailure("m_buttons <missing>");
            HealthReporting.ReportKeybindInspectionFailure("second inspection failure");
            Require(
                HealthReporting.KeybindInspectionDetail?.Contains("m_buttons <missing>", StringComparison.Ordinal) == true,
                "first keybind inspection failure is retained");
            Require(BenheimQoL.Plugin.Log.Warnings.Count == 1, "keybind warning is deduplicated");
            Require(BenheimQoL.Infrastructure.Diagnostics.Events.Count == 1, "keybind diagnostic is deduplicated");

            HealthReporting.ReportPatchGroupFailure(
                "Farming",
                "BenheimQoL.Farming.BrokenPatch",
                new InvalidOperationException("target Player.MissingHook was not found"));
            HealthReporting.ReportPatchGroupFailure(
                "Farming",
                "BenheimQoL.Farming.SecondBrokenPatch",
                new InvalidOperationException("second hook failure"));
            Require(HealthReporting.GameplayActionsEnabled,
                "one patch group failure leaves unrelated gameplay enabled");
            Require(HealthReporting.PatchGroupFailures.Count == 1,
                "a failed patch group is reported once");
            Require(
                HealthReporting.PatchGroupFailures[0].PatchType == "BenheimQoL.Farming.BrokenPatch"
                    && HealthReporting.PatchGroupFailures[0].Error.Contains("Player.MissingHook", StringComparison.Ordinal),
                "the exact patch type and failure remain visible");
            Require(BenheimQoL.Plugin.Log.Errors.Count == 1,
                "the patch group writes one exact error");
            Require(BenheimQoL.Infrastructure.Diagnostics.TypedEvents.Count == 1,
                "the patch group typed diagnostic is deduplicated");
            DiagnosticEvent patchFailure = BenheimQoL.Infrastructure.Diagnostics.TypedEvents[0];
            Require(
                patchFailure.Domain == "Health"
                    && patchFailure.Name == "patch_group_disabled"
                    && patchFailure.Fields["owner"] == "Farming"
                    && patchFailure.Fields["patch_type"] == "BenheimQoL.Farming.BrokenPatch"
                    && patchFailure.Fields["error_type"] == nameof(InvalidOperationException)
                    && patchFailure.Fields["error"] == "target Player.MissingHook was not found",
                "the startup failure is typed with its exact patch and base exception");

            HealthReporting.ReportPatchCleanupFailure(
                "Farming",
                new ApplicationException(
                    "cleanup wrapper",
                    new InvalidOperationException("Harmony owner could not be removed")));
            Require(BenheimQoL.Plugin.Log.Errors.Count == 2,
                "the cleanup failure keeps its normal BepInEx error");
            Require(BenheimQoL.Infrastructure.Diagnostics.TypedEvents.Count == 2,
                "the cleanup failure emits one typed diagnostic");
            DiagnosticEvent cleanupFailure = BenheimQoL.Infrastructure.Diagnostics.TypedEvents[1];
            Require(
                cleanupFailure.Domain == "Health"
                    && cleanupFailure.Name == "partial_patch_cleanup_failed"
                    && cleanupFailure.Fields["owner"] == "Farming"
                    && cleanupFailure.Fields["error_type"] == nameof(InvalidOperationException)
                    && cleanupFailure.Fields["error"] == "Harmony owner could not be removed",
                "the cleanup failure is typed with its owner and exact base exception");

            HealthReporting.ReportPatchCleanupSucceeded("Farming");
            Require(BenheimQoL.Plugin.Log.Infos.Count == 1,
                "cleanup recovery keeps its normal BepInEx info");
            Require(BenheimQoL.Infrastructure.Diagnostics.TypedEvents.Count == 3,
                "cleanup recovery emits one typed terminal outcome");
            DiagnosticEvent cleanupRecovery = BenheimQoL.Infrastructure.Diagnostics.TypedEvents[2];
            Require(
                cleanupRecovery.Domain == "Health"
                    && cleanupRecovery.Name == "partial_patches_removed"
                    && cleanupRecovery.Fields["owner"] == "Farming",
                "the cleanup recovery is typed with its owner");

            Player.m_localPlayer = new Player { ThrowNextMessage = true };
            HealthReporting.UpdateCriticalMessage();
            HealthReporting.UpdateCriticalMessage();
            HealthReporting.UpdateCriticalMessage();
            Require(Player.m_localPlayer.MessageCount == 1,
                "the feature warning retries once and is shown once per session");

            HealthReporting.DisableCore(new InvalidOperationException("hook <missing>"));
            HealthReporting.DisableCore(new InvalidOperationException("second hook failure"));
            Require(!HealthReporting.GameplayActionsEnabled, "core failure disables gameplay actions");
            Require(BenheimQoL.Plugin.Log.Errors.Count == 3, "core error is deduplicated");
            Require(BenheimQoL.Infrastructure.Diagnostics.Events.Count == 2, "core diagnostic is deduplicated");

            HealthReporting.UpdateCriticalMessage();
            HealthReporting.UpdateCriticalMessage();
            HealthReporting.UpdateCriticalMessage();
            Require(Player.m_localPlayer.MessageCount == 2, "the global critical message is shown once per session");

            HealthReporting.BeginSession();
            Require(HealthReporting.GameplayActionsEnabled, "session reset re-enables gameplay actions");
            Require(HealthReporting.KeybindInspectionDetail == null, "session reset clears old warnings");
            Require(HealthReporting.PatchGroupFailures.Count == 0, "session reset clears old patch group failures");

            HealthReporting.ReportKillAttributionUnavailable("capability timeout");
            HealthReporting.ReportKillAttributionUnavailable("capability timeout");
            Require(
                HealthReporting.KillAttributionDetail?.Contains("capability timeout", StringComparison.Ordinal) == true,
                "kill attribution failure remains visible while capability is unavailable");
            Require(BenheimQoL.Plugin.Log.Warnings.Count == 3, "identical capability warnings are deduplicated");

            HealthReporting.UpdateCriticalMessage();
            HealthReporting.UpdateCriticalMessage();
            Require(
                Player.m_localPlayer.MessageCount == 3,
                "the BERSERKER compatibility warning is shown once per session");

            HealthReporting.ReportKillAttributionUnavailable("new connection pending");
            Require(
                HealthReporting.KillAttributionDetail?.Contains("new connection pending", StringComparison.Ordinal) == true,
                "a new connection cannot hide a genuine capability warning before a match arrives");

            HealthReporting.ReportKillAttributionAvailable();
            Require(
                HealthReporting.KillAttributionDetail == null,
                "a matching capability clears the connection warning");
            HealthReporting.ReportKillAttributionUnavailable("new connection timeout");
            HealthReporting.UpdateCriticalMessage();
            Require(
                Player.m_localPlayer.MessageCount == 3,
                "a later connection warning does not replay the session message");

            Console.WriteLine("health reporting state, feature isolation, fail-closed, and message checks passed");
            return 0;
        }

        private static void Require(bool condition, string scenario)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"failed: {scenario}");
            }
        }
    }
}
