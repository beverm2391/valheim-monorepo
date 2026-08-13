using BenheimQoL.Infrastructure;
using System;
using System.IO;
using System.Text;

namespace BenheimTestCommands;

internal static class ServerDiagnostics
{
    internal const string EventFileName = "BenheimTestCommandEvents.ndjson";

    private static StreamWriter? writer;
    private static string session = string.Empty;
    private static string version = string.Empty;
    private static bool failureLogged;

    internal static void Begin(string bepinExRootPath, string pluginVersion)
    {
        End();
        session = Guid.NewGuid().ToString("N");
        version = pluginVersion;
        failureLogged = false;
        try
        {
            writer = new StreamWriter(
                Path.Combine(bepinExRootPath, EventFileName),
                append: false,
                new UTF8Encoding(false))
            {
                AutoFlush = true
            };
        }
        catch (Exception exception)
        {
            LogFailure(exception);
        }
    }

    internal static void Emit(DiagnosticEvent diagnosticEvent)
    {
        diagnosticEvent.Prepare(DateTime.UtcNow, session, version);
        Plugin.Log.LogInfo(diagnosticEvent.ToReadableLine());
        if (writer == null)
        {
            return;
        }

        try
        {
            writer.WriteLine(diagnosticEvent.ToJsonLine());
        }
        catch (Exception exception)
        {
            writer.Dispose();
            writer = null;
            LogFailure(exception);
        }
    }

    internal static void End()
    {
        writer?.Dispose();
        writer = null;
    }

    private static void LogFailure(Exception exception)
    {
        if (failureLogged)
        {
            return;
        }

        failureLogged = true;
        Plugin.Log.LogWarning($"Benheim test-command structured diagnostics are unavailable: {exception.GetType().Name}");
    }
}
