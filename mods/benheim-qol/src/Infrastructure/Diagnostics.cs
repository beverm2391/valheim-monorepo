namespace BenheimQoL.Infrastructure;

internal static class Diagnostics
{
    internal static void Event(string feature, string action, string details = "")
    {
        string suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" {details}";
        Plugin.Log.LogInfo($"[diag][{feature}] {action}{suffix}");
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
}
