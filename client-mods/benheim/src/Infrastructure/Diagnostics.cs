using System;
using System.IO;
using System.Text;

namespace BenheimQoL.Infrastructure;

internal static class Diagnostics
{
    internal const string CurrentEventFileName = "BenheimEvents.ndjson";

    private static readonly object Gate = new object();
    private static readonly DiagnosticEventRouter OptionalDestinations =
        new DiagnosticEventRouter(
            new DiagnosticEventRoute(SelectEveryTypedEvent, RemoteDiagnostics.TryEnqueue));
    private static StreamWriter? eventWriter;
    private static string sessionId = string.Empty;
    private static string benheimVersion = string.Empty;
    private static bool writeFailureLogged;
    private static Action<string, string, string>? externalObserver;

    internal static void BeginSession(string bepinExRootPath, string version)
    {
        lock (Gate)
        {
            eventWriter?.Dispose();
            eventWriter = null;
            sessionId = Guid.NewGuid().ToString("N");
            benheimVersion = version;
            writeFailureLogged = false;
            try
            {
                string path = Path.Combine(bepinExRootPath, CurrentEventFileName);
                eventWriter = new StreamWriter(path, append: false, new UTF8Encoding(false))
                {
                    AutoFlush = true
                };
            }
            catch (Exception exception)
            {
                LogWriteFailure(exception);
            }
        }
    }

    internal static void EndSession()
    {
        lock (Gate)
        {
            eventWriter?.Dispose();
            eventWriter = null;
            externalObserver = null;
        }
    }

    internal static void Event(string feature, string action, string details = "")
    {
        string suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" {details}";
        Plugin.Log.LogInfo($"[diag][{feature}] {action}{suffix}");
    }

    internal static void Emit(DiagnosticEvent diagnosticEvent)
    {
        Action<string, string, string>? observer;
        lock (Gate)
        {
            diagnosticEvent.Prepare(DateTime.UtcNow, sessionId, benheimVersion);
            Plugin.Log.LogInfo(diagnosticEvent.ToReadableLine());
            if (eventWriter != null)
            {
                try
                {
                    eventWriter.WriteLine(diagnosticEvent.ToJsonLine());
                }
                catch (Exception exception)
                {
                    eventWriter.Dispose();
                    eventWriter = null;
                    LogWriteFailure(exception);
                }
            }
            observer = externalObserver;
        }

        // Local readable and NDJSON emission above is always complete before
        // independently selected optional destinations observe the same whole
        // typed event. Destinations own transport, not event definition.
        OptionalDestinations.Route(diagnosticEvent);
        if (observer != null)
        {
            try
            {
                observer(diagnosticEvent.Domain, diagnosticEvent.Name, diagnosticEvent.ToJsonLine());
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning(
                    $"Benheim external evidence observation failed: {Flatten(exception.Message)}");
            }
        }
    }

    // The optional Valheim Dev adapter is the only caller. Keeping the raw
    // subscription here lets focused diagnostics tests compile this event
    // writer without pulling in Benheim's health-reporting subsystem.
    internal static void SetExternalObserver(Action<string, string, string>? observer)
    {
        lock (Gate)
        {
            externalObserver = observer;
        }
    }

    private static bool SelectEveryTypedEvent(DiagnosticEvent _) => true;

    internal static string NewOperationId()
    {
        return Guid.NewGuid().ToString("N");
    }

    internal static string Bool(bool value)
    {
        return value ? "true" : "false";
    }

    internal static string Flatten(string value)
    {
        return value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace(' ', '_');
    }

    private static void LogWriteFailure(Exception exception)
    {
        if (writeFailureLogged)
        {
            return;
        }

        writeFailureLogged = true;
        Plugin.Log.LogWarning($"Benheim structured diagnostics are unavailable: {Flatten(exception.Message)}");
    }
}
