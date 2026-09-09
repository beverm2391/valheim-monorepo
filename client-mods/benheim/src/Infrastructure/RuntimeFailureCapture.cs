using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Logging;

namespace BenheimQoL.Infrastructure;

/// <summary>
/// Converts bounded, actionable failures from BepInEx's existing log bus into
/// normal typed diagnostics. The original BepInEx log remains the raw source;
/// this class only forwards a redacted record that can be queried remotely.
/// </summary>
internal static class RuntimeFailureCapture
{
    internal const int MaximumDistinctFailures = 64;
    internal const int MaximumMessageCharacters = 1024;
    internal const int MaximumStackCharacters = 4096;

    private const int MaximumPendingFailures = 64;
    private const int MaximumStartupLogCharacters = 262144;
    private const int MaximumFailuresPerUpdate = 8;
    private const string ActiveLogFileName = "LogOutput.log";

    private static readonly object Gate = new object();
    private static readonly Queue<RuntimeFailure> Pending = new Queue<RuntimeFailure>();
    private static readonly HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Regex Credential = new Regex(
        @"(?i)\b(token|password|secret|authorization|api[_-]?key)\s*[:=]\s*(?:""[^""]*""|'[^']*'|[^\s,;]+)",
        RegexOptions.CultureInvariant);
    private static readonly Regex Url = new Regex(
        @"(?i)\bhttps?://[^\s]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex WindowsPath = new Regex(
        @"(?i)\b[a-z]:\\(?:[^\s\\/:*?""<>|]+\\)+[^\s\\:*?""<>|]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex UnixPath = new Regex(
        @"(?<![\w.])/(?:[^\s/:]+/)+[^\s:]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex Ipv4 = new Regex(
        @"(?<!\d)(?:\d{1,3}\.){3}\d{1,3}(?::\d{1,5})?(?!\d)",
        RegexOptions.CultureInvariant);
    private static readonly Regex LongIdentifier = new Regex(
        @"(?<!\d)\d{15,20}(?!\d)",
        RegexOptions.CultureInvariant);

    private static FailureLogListener? listener;
    private static bool limitReached;
    private static bool limitReported;

    internal static void Begin(string bepinExRootPath)
    {
        End();
        lock (Gate)
        {
            Pending.Clear();
            Seen.Clear();
            limitReached = false;
            limitReported = false;
        }

        // Register first so failures that arrive while the startup prefix is
        // inspected cannot fall into a gap. Signature deduplication collapses
        // a record if it is observed by both paths.
        listener = new FailureLogListener();
        Logger.Listeners.Add(listener);
        ScanStartupPrefix(Path.Combine(bepinExRootPath, ActiveLogFileName));
    }

    internal static void Update()
    {
        List<RuntimeFailure> failures = new List<RuntimeFailure>(MaximumFailuresPerUpdate);
        bool reportLimit = false;
        lock (Gate)
        {
            while (failures.Count < MaximumFailuresPerUpdate && Pending.Count > 0)
            {
                failures.Add(Pending.Dequeue());
            }

            if (limitReached && !limitReported && Pending.Count == 0)
            {
                limitReported = true;
                reportLimit = true;
            }
        }

        foreach (RuntimeFailure failure in failures)
        {
            Diagnostics.Emit(failure.ToDiagnosticEvent());
        }

        if (reportLimit)
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("Health", "runtime_failure_capture_limited")
                    .Integer("distinct_failure_limit", MaximumDistinctFailures));
        }
    }

    internal static void End()
    {
        FailureLogListener? current = listener;
        listener = null;
        if (current != null)
        {
            Logger.Listeners.Remove(current);
            current.Dispose();
        }
    }

    private static void ScanStartupPrefix(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            using FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using StreamReader reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: false);

            string? severity = null;
            string? logger = null;
            StringBuilder? record = null;
            int charactersRead = 0;
            while (charactersRead < MaximumStartupLogCharacters)
            {
                string? line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }

                charactersRead += line.Length + 1;
                if (TryParseLogHeader(line, out string parsedSeverity, out string parsedLogger, out string message))
                {
                    EnqueueParsedRecord(severity, logger, record, "startup_log");
                    severity = parsedSeverity;
                    logger = parsedLogger;
                    record = new StringBuilder(message);
                    continue;
                }

                if (record != null && record.Length < MaximumStackCharacters * 2)
                {
                    record.Append('\n').Append(line);
                }
            }

            EnqueueParsedRecord(severity, logger, record, "startup_log");
        }
        catch
        {
            // The full local log remains the fallback. Failure capture must
            // never interfere with plugin startup or create recursive logging.
        }
    }

    private static void EnqueueParsedRecord(
        string? severity,
        string? logger,
        StringBuilder? record,
        string origin)
    {
        if (severity == null || logger == null || record == null)
        {
            return;
        }

        Enqueue(logger, severity, record.ToString(), origin);
    }

    private static bool TryParseLogHeader(
        string line,
        out string severity,
        out string logger,
        out string message)
    {
        severity = string.Empty;
        logger = string.Empty;
        message = string.Empty;
        if (line.Length < 5 || line[0] != '[')
        {
            return false;
        }

        int colon = line.IndexOf(':');
        int close = line.IndexOf(']');
        if (colon <= 1 || close <= colon)
        {
            return false;
        }

        severity = line.Substring(1, colon - 1).Trim();
        logger = line.Substring(colon + 1, close - colon - 1).Trim();
        message = close + 1 < line.Length ? line.Substring(close + 1).TrimStart() : string.Empty;
        return true;
    }

    private static void Enqueue(string logger, string severity, string raw, string origin)
    {
        if (!IsActionable(severity, raw))
        {
            return;
        }

        string source = string.Equals(logger, "Unity Log", StringComparison.OrdinalIgnoreCase)
            ? "unity"
            : "bepinex";
        SplitRecord(raw, out string rawMessage, out string rawStack);
        string message = Sanitize(rawMessage, MaximumMessageCharacters);
        string stack = Sanitize(rawStack, MaximumStackCharacters);
        if (message.Length == 0)
        {
            return;
        }

        string fingerprint = Fingerprint(source, severity, logger, message, stack);
        lock (Gate)
        {
            if (Seen.Contains(fingerprint))
            {
                return;
            }

            if (Seen.Count >= MaximumDistinctFailures || Pending.Count >= MaximumPendingFailures)
            {
                limitReached = true;
                return;
            }

            Seen.Add(fingerprint);
            Pending.Enqueue(new RuntimeFailure(
                source,
                origin,
                severity.ToLowerInvariant(),
                Sanitize(logger, 128),
                message,
                stack,
                fingerprint));
        }
    }

    private static bool IsActionable(string severity, string raw)
    {
        if (string.Equals(severity, "Error", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(severity, "Fatal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(severity, "Warning", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string message = raw.ToLowerInvariant();
        return message.Contains("exception") ||
            message.Contains("failed") ||
            message.Contains("failure") ||
            message.Contains("could not") ||
            message.Contains("unable to") ||
            message.Contains("missing dependency") ||
            message.Contains("incompatible");
    }

    private static void SplitRecord(string raw, out string message, out string stack)
    {
        int newline = raw.IndexOfAny(new[] { '\r', '\n' });
        if (newline < 0)
        {
            message = raw;
            stack = string.Empty;
            return;
        }

        message = raw.Substring(0, newline);
        stack = raw.Substring(newline).TrimStart('\r', '\n');
    }

    internal static string Sanitize(string value, int maximumCharacters)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        int inspectionLimit = Math.Min(value.Length, maximumCharacters * 4);
        string sanitized = value.Substring(0, inspectionLimit)
            .Replace('\0', ' ');
        sanitized = Credential.Replace(sanitized, "$1=[redacted]");
        sanitized = Url.Replace(sanitized, "<url>");
        sanitized = WindowsPath.Replace(sanitized, "<local_path>");
        sanitized = UnixPath.Replace(sanitized, "<local_path>");
        sanitized = Ipv4.Replace(sanitized, "<ip>");
        sanitized = LongIdentifier.Replace(sanitized, "<identifier>");

        StringBuilder printable = new StringBuilder(Math.Min(sanitized.Length, maximumCharacters));
        foreach (char character in sanitized)
        {
            if (printable.Length >= maximumCharacters)
            {
                break;
            }

            if (character == '\r' || character == '\n' || character == '\t' || !char.IsControl(character))
            {
                printable.Append(character);
            }
        }

        return printable.ToString().Trim();
    }

    private static string Fingerprint(
        string source,
        string severity,
        string logger,
        string message,
        string stack)
    {
        // FNV-1a gives a stable, non-secret grouping key without carrying the
        // full source record into the searchable envelope.
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        string value = source + "\n" + severity + "\n" + logger + "\n" + message + "\n" + stack;
        for (int index = 0; index < value.Length; index++)
        {
            hash ^= value[index];
            hash *= prime;
        }
        return hash.ToString("x16");
    }

    private sealed class FailureLogListener : ILogListener
    {
        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            string logger = eventArgs.Source?.SourceName ?? "unknown";
            string raw = eventArgs.Data?.ToString() ?? string.Empty;
            Enqueue(logger, eventArgs.Level.GetHighestLevel().ToString(), raw, "live");
        }

        public void Dispose()
        {
        }
    }

    private readonly struct RuntimeFailure
    {
        internal RuntimeFailure(
            string source,
            string origin,
            string severity,
            string logger,
            string message,
            string stack,
            string fingerprint)
        {
            Source = source;
            Origin = origin;
            Severity = severity;
            Logger = logger;
            Message = message;
            Stack = stack;
            Fingerprint = fingerprint;
        }

        private string Source { get; }
        private string Origin { get; }
        private string Severity { get; }
        private string Logger { get; }
        private string Message { get; }
        private string Stack { get; }
        private string Fingerprint { get; }

        internal DiagnosticEvent ToDiagnosticEvent()
        {
            DiagnosticEvent diagnosticEvent = DiagnosticEvent.Create("Health", "runtime_failure")
                .String("failure_source", Source)
                .String("capture_origin", Origin)
                .String("severity", Severity)
                .String("logger", Logger)
                .String("message", Message)
                .String("fingerprint", Fingerprint);
            if (Stack.Length > 0)
            {
                diagnosticEvent.String("stack_trace", Stack);
            }
            return diagnosticEvent;
        }
    }
}
