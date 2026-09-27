using System;
using System.IO;
using BepInEx.Configuration;
using BenheimQoL.Infrastructure;

string configPath = Path.Combine(
    Path.GetTempPath(),
    $"benheim-diagnostics-settings-{Guid.NewGuid():N}.cfg");
try
{
    ConfigFile first = new ConfigFile(configPath, saveOnInit: true);
    // An old opt-out can remain on disk, but it no longer governs the group
    // client. Only identity and the one-time notice are current settings.
    first.Bind("Diagnostics", "Share Diagnostics", false, "retired setting").Value = false;
    DiagnosticsClientSettings.Initialize(first);
    string clientId = DiagnosticsClientSettings.ClientId;
    Expect(Guid.TryParseExact(clientId, "N", out _), "client ID is a GUID");
    Expect(!DiagnosticsClientSettings.NoticeShown, "notice begins pending");
    DiagnosticsClientSettings.MarkNoticeShown();

    ConfigFile later = new ConfigFile(configPath, saveOnInit: true);
    DiagnosticsClientSettings.Initialize(later);
    Expect(DiagnosticsClientSettings.ClientId == clientId, "client ID persists");
    Expect(DiagnosticsClientSettings.NoticeShown, "one-time notice persists");
}
finally
{
    if (File.Exists(configPath))
    {
        File.Delete(configPath);
    }
}

Console.WriteLine("diagnostics identity and notice checks passed");

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
