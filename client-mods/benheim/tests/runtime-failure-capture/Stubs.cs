using System;
using System.Collections.Generic;

namespace BepInEx.Logging
{
    [Flags]
    internal enum LogLevel
    {
        None = 0,
        Fatal = 1,
        Error = 2,
        Warning = 4,
        Message = 8,
        Info = 16,
        Debug = 32,
        All = Fatal | Error | Warning | Message | Info | Debug
    }

    internal static class LogLevelExtensions
    {
        internal static LogLevel GetHighestLevel(this LogLevel level)
        {
            foreach (LogLevel candidate in new[]
            {
                LogLevel.Fatal,
                LogLevel.Error,
                LogLevel.Warning,
                LogLevel.Message,
                LogLevel.Info,
                LogLevel.Debug
            })
            {
                if ((level & candidate) != 0)
                {
                    return candidate;
                }
            }
            return LogLevel.None;
        }
    }

    internal interface ILogSource
    {
        string SourceName { get; }
    }

    internal interface ILogListener : IDisposable
    {
        void LogEvent(object sender, LogEventArgs eventArgs);
    }

    internal sealed class LogEventArgs : EventArgs
    {
        internal LogEventArgs(object data, LogLevel level, ILogSource source)
        {
            Data = data;
            Level = level;
            Source = source;
        }

        internal object Data { get; }
        internal LogLevel Level { get; }
        internal ILogSource Source { get; }
    }

    internal static class Logger
    {
        internal static List<ILogListener> Listeners { get; } = new List<ILogListener>();
    }

    internal sealed class TestLogSource : ILogSource
    {
        internal TestLogSource(string sourceName)
        {
            SourceName = sourceName;
        }

        public string SourceName { get; }
    }
}

namespace BenheimQoL.Infrastructure
{
    internal static class Diagnostics
    {
        internal static List<string> Emitted { get; } = new List<string>();

        internal static void Emit(DiagnosticEvent diagnosticEvent)
        {
            diagnosticEvent.Prepare(
                new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(Emitted.Count),
                "test-session",
                "test-version");
            Emitted.Add(diagnosticEvent.ToJsonLine());
        }
    }
}
