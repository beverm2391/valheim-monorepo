using System;
using System.Text;
using System.Text.RegularExpressions;

namespace BenheimQoL.Infrastructure;

/// <summary>
/// Removes data that a raw log may carry but the private typed diagnostics
/// contract does not permit. Apply this before constructing an event because
/// the general typed-event serializer intentionally preserves producer fields.
/// </summary>
internal static class RuntimeFailurePrivacy
{
    private static readonly Regex Credential = new Regex(
        @"(?i)\b(token|password|secret|authorization|api[_-]?key)\s*[:=]\s*(?:""[^""]*""|'[^']*'|(?:bearer\s+)?[^\s,;]+)",
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

    internal static string Fingerprint(
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
}
