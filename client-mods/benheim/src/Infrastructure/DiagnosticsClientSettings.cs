using BepInEx.Configuration;
using System;

namespace BenheimQoL.Infrastructure;

internal static class DiagnosticsClientSettings
{
    private const string Section = "Diagnostics";
    private static ConfigEntry<bool>? noticeShown;
    private static ConfigEntry<string>? clientId;

    internal static bool NoticeShown => noticeShown?.Value ?? false;
    internal static string ClientId { get; private set; } = string.Empty;

    internal static void Initialize(ConfigFile config)
    {
        noticeShown = config.Bind(
            Section,
            "Sharing Notice Shown",
            false,
            "Tracks whether Benheim showed the one-time group diagnostics notice.");
        clientId = config.Bind(
            Section,
            "Pseudonymous Client ID",
            string.Empty,
            "Random local identifier used to correlate group diagnostics.");

        ClientId = Guid.TryParseExact(clientId.Value, "N", out Guid parsed)
            ? parsed.ToString("N")
            : Guid.NewGuid().ToString("N");
        if (clientId.Value != ClientId)
        {
            clientId.Value = ClientId;
        }
    }

    internal static void MarkNoticeShown()
    {
        if (noticeShown != null)
        {
            noticeShown.Value = true;
        }
    }
}
