using System;
using System.IO;
using BepInEx;

namespace BenheimQoL.GreydwarfResident;

internal static class ResidentSpeechSettings
{
    internal const string PrivateConfigFileName = "GEORGE-SPEECH.cfg";

    private const string ConfigMarker = "BENHEIM_PRIVATE_SPEECH_V1";
    private static readonly Lazy<string?> LoadedApiKey = new Lazy<string?>(ReadPrivateApiKey);

    internal static string ApiKey => LoadedApiKey.Value ?? string.Empty;

    private static string? ReadPrivateApiKey()
    {
        string path = Path.Combine(Paths.ConfigPath, PrivateConfigFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            FileInfo info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > 4096)
            {
                Warn("has an invalid size");
                return null;
            }

            string[] lines = File.ReadAllLines(path);
            if (lines.Length != 2 || lines[0] != ConfigMarker)
            {
                Warn("has an invalid format");
                return null;
            }

            string apiKey = ReadValue(lines[1], "api_key=");
            if (!ValidApiKey(apiKey))
            {
                Warn("is invalid");
                return null;
            }

            return apiKey;
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning(
                $"Benheim resident speech config could not be read; resident speech is unavailable ({exception.GetType().Name}).");
            return null;
        }
    }

    private static string ReadValue(string line, string prefix)
    {
        return line.StartsWith(prefix, StringComparison.Ordinal)
            ? line.Substring(prefix.Length)
            : string.Empty;
    }

    private static bool ValidApiKey(string apiKey)
    {
        if (apiKey.Length == 0 || apiKey.Length > 1024)
        {
            return false;
        }

        foreach (char character in apiKey)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                return false;
            }
        }

        return true;
    }

    private static void Warn(string reason)
    {
        Plugin.Log.LogWarning(
            $"Benheim resident speech config {reason}; resident speech is unavailable.");
    }
}
