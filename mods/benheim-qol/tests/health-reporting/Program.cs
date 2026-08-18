using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;

namespace BepInEx.Logging
{
    internal sealed class ManualLogSource
    {
        internal readonly List<string> Errors = new();
        internal readonly List<string> Warnings = new();

        internal void LogError(string message) => Errors.Add(message);
        internal void LogWarning(string message) => Warnings.Add(message);
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
    internal static class Diagnostics
    {
        internal static readonly List<string> Events = new();

        internal static void Event(string feature, string action, string details = "")
        {
            Events.Add($"[diag][{feature}] {action} {details}".TrimEnd());
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

            HealthReporting.DisableCore(new InvalidOperationException("hook <missing>"));
            HealthReporting.DisableCore(new InvalidOperationException("second hook failure"));
            Require(!HealthReporting.GameplayActionsEnabled, "core failure disables gameplay actions");
            Require(BenheimQoL.Plugin.Log.Errors.Count == 1, "core error is deduplicated");
            Require(BenheimQoL.Infrastructure.Diagnostics.Events.Count == 2, "core diagnostic is deduplicated");

            Player.m_localPlayer = new Player { ThrowNextMessage = true };
            HealthReporting.UpdateCriticalMessage();
            HealthReporting.UpdateCriticalMessage();
            HealthReporting.UpdateCriticalMessage();
            Require(Player.m_localPlayer.MessageCount == 1, "critical message retries once and is shown once per session");

            HealthReporting.BeginSession();
            Require(HealthReporting.GameplayActionsEnabled, "session reset re-enables gameplay actions");
            Require(HealthReporting.KeybindInspectionDetail == null, "session reset clears old warnings");

            HealthReporting.ReportKillAttributionUnavailable("capability timeout");
            HealthReporting.ReportKillAttributionUnavailable("capability timeout");
            Require(
                HealthReporting.KillAttributionDetail?.Contains("capability timeout", StringComparison.Ordinal) == true,
                "kill attribution failure remains visible while capability is unavailable");
            Require(BenheimQoL.Plugin.Log.Warnings.Count == 3, "identical capability warnings are deduplicated");

            HealthReporting.UpdateCriticalMessage();
            HealthReporting.UpdateCriticalMessage();
            Require(
                Player.m_localPlayer.MessageCount == 2,
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
                Player.m_localPlayer.MessageCount == 2,
                "a later connection warning does not replay the session message");

            Console.WriteLine("health reporting state, deduplication, fail-closed, and message checks passed");
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
