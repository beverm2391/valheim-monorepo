using BepInEx.Configuration;
using System;

namespace BenheimQoL.Infrastructure;

internal static class DiagnosticsSharingSettings
{
    private const string Section = "Diagnostics";
    private static ConfigEntry<bool>? shareDiagnostics;
    private static ConfigEntry<bool>? noticeShown;
    private static ConfigEntry<string>? clientId;

    internal static bool ShareDiagnostics => shareDiagnostics?.Value ?? true;
    internal static bool NoticeShown => noticeShown?.Value ?? false;
    internal static string ClientId { get; private set; } = string.Empty;

    internal static void Initialize(ConfigFile config)
    {
        shareDiagnostics = config.Bind(
            Section,
            "Share Diagnostics",
            true,
            "Share privacy-filtered typed Benheim events in configured private test builds.");
        noticeShown = config.Bind(
            Section,
            "Sharing Notice Shown",
            false,
            "Tracks whether Benheim showed the one-time private diagnostics notice.");
        clientId = config.Bind(
            Section,
            "Pseudonymous Client ID",
            string.Empty,
            "Random local identifier used only to correlate private test diagnostics.");

        ClientId = Guid.TryParseExact(clientId.Value, "N", out Guid parsed)
            ? parsed.ToString("N")
            : Guid.NewGuid().ToString("N");
        if (clientId.Value != ClientId)
        {
            clientId.Value = ClientId;
        }
    }

    internal static void SetShareDiagnostics(bool enabled)
    {
        if (shareDiagnostics != null)
        {
            shareDiagnostics.Value = enabled;
        }
        RemoteDiagnostics.SetSharingEnabled(enabled);
    }

    internal static void MarkNoticeShown()
    {
        if (noticeShown != null)
        {
            noticeShown.Value = true;
        }
    }
}
