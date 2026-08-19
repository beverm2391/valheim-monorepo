using System;
using System.IO;
using BepInEx.Configuration;
using BenheimQoL.Infrastructure;

string configPath = Path.Combine(
    Path.GetTempPath(),
    $"benheim-diagnostics-sharing-{Guid.NewGuid():N}.cfg");
try
{
    ConfigFile legacy = new ConfigFile(configPath, saveOnInit: true);
    legacy.Bind(
        "Diagnostics",
        "Share Diagnostics",
        true,
        "legacy setting").Value = false;
    legacy.Save();

    DiagnosticsSharingSettings.Initialize(legacy);
    DiagnosticsSharingSettings.ApplyLegacyPrivateTestDefault(
        privateTestConfigured: false);
    Expect(!DiagnosticsSharingSettings.ShareDiagnostics,
        "a nonconfigured build must preserve the legacy false value");
    Expect(!DiagnosticsSharingSettings.LegacyPrivateDefaultMigrated,
        "a nonconfigured build must leave the private-test migration pending");

    ConfigFile firstPrivateRun = new ConfigFile(configPath, saveOnInit: true);
    DiagnosticsSharingSettings.Initialize(firstPrivateRun);
    DiagnosticsSharingSettings.ApplyLegacyPrivateTestDefault(
        privateTestConfigured: true);
    Expect(DiagnosticsSharingSettings.ShareDiagnostics,
        "the first configured private-test run migrates a legacy false value to the on default");
    Expect(DiagnosticsSharingSettings.LegacyPrivateDefaultMigrated,
        "the first configured private-test run records migration completion");

    DiagnosticsSharingSettings.SetShareDiagnostics(false);
    Expect(RemoteDiagnostics.LastSharingValue == false,
        "an explicit opt-out still reaches the live sharing sink");

    ConfigFile laterPrivateRun = new ConfigFile(configPath, saveOnInit: true);
    DiagnosticsSharingSettings.Initialize(laterPrivateRun);
    DiagnosticsSharingSettings.ApplyLegacyPrivateTestDefault(
        privateTestConfigured: true);
    Expect(!DiagnosticsSharingSettings.ShareDiagnostics,
        "a later private-test run preserves the player's post-migration opt-out");
    Expect(DiagnosticsSharingSettings.LegacyPrivateDefaultMigrated,
        "the one-time migration marker remains complete");
}
finally
{
    if (File.Exists(configPath))
    {
        File.Delete(configPath);
    }
}

Console.WriteLine("diagnostics sharing one-time migration checks passed");

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
