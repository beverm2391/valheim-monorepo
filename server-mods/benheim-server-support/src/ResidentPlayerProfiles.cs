using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BenheimServerSupport;

/// <summary>
/// Loads and validates the private operator-authored player profiles. This
/// file owns the config path, schema, bounds, and parsing failures; it never
/// serializes account keys into provider context.
/// </summary>
internal sealed class ResidentPlayerProfiles
{
    private const int SchemaVersion = 1;
    private const int MaximumMapCharacters = 64 * 1024;
    private const int MaximumProfileCount = 64;
    private const int MaximumIdentityCharacters = 256;
    private const int MaximumPreferredNameCharacters = 64;
    private const int MaximumGenderCharacters = 32;
    private const int MaximumPronounCharacters = 32;
    private const int MaximumLoreCharacters = 512;

    private static readonly string[] MapFields = { "schemaVersion", "players" };
    private static readonly string[] ProfileFields = { "preferredName", "gender", "pronouns", "lore" };

    private readonly Dictionary<string, ResidentPlayerProfile> profiles;

    private ResidentPlayerProfiles(Dictionary<string, ResidentPlayerProfile> profiles)
    {
        this.profiles = profiles;
    }

    private static string ConfiguredPath => Path.Combine(
        Paths.ConfigPath,
        "Benheim",
        "GreydwarfResident",
        "players.json");

    internal static ResidentPlayerProfiles LoadConfigured() => Load(ConfiguredPath);

    internal static ResidentPlayerProfiles Load(string mapFilePath)
    {
        if (string.IsNullOrWhiteSpace(mapFilePath))
        {
            throw new ArgumentException("A player profile map path is required.", nameof(mapFilePath));
        }

        string fullPath = Path.GetFullPath(mapFilePath);
        if (!File.Exists(fullPath))
        {
            return Empty();
        }

        string json;
        try
        {
            json = File.ReadAllText(fullPath, new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true));
        }
        catch (DecoderFallbackException exception)
        {
            throw new FormatException("Player profile map is not valid UTF-8.", exception);
        }

        if (json.Length == 0 || json.Length > MaximumMapCharacters)
        {
            throw new FormatException("Player profile map size is invalid.");
        }

        JObject root = ParseObject(json, "Player profile map");
        if (!HasAllowedFields(root, MapFields, exact: true)
            || !TryInteger(root["schemaVersion"], out int schemaVersion)
            || schemaVersion != SchemaVersion
            || root["players"] is not JObject serializedPlayers
            || serializedPlayers.Properties().Count() > MaximumProfileCount)
        {
            throw new FormatException("Player profile map fields or schema version are invalid.");
        }

        var profiles = new Dictionary<string, ResidentPlayerProfile>(StringComparer.Ordinal);
        foreach (JProperty property in serializedPlayers.Properties())
        {
            string identity = property.Name;
            if (!IsValidIdentityKey(identity)
                || property.Value is not JObject serializedProfile
                || !HasAllowedFields(serializedProfile, ProfileFields, exact: false))
            {
                throw new FormatException("Player profile map contains an invalid profile entry.");
            }

            if (!TryOptionalString(serializedProfile, "preferredName", MaximumPreferredNameCharacters, false, out string? preferredName)
                || !TryOptionalString(serializedProfile, "gender", MaximumGenderCharacters, false, out string? gender)
                || !TryOptionalString(serializedProfile, "pronouns", MaximumPronounCharacters, false, out string? pronouns)
                || !TryOptionalString(serializedProfile, "lore", MaximumLoreCharacters, true, out string? lore))
            {
                throw new FormatException("Player profile map contains an invalid profile field.");
            }

            profiles.Add(identity, new ResidentPlayerProfile(preferredName, gender, pronouns, lore));
        }

        return new ResidentPlayerProfiles(profiles);
    }

    internal bool TryGet(string identity, out ResidentPlayerProfile profile) =>
        profiles.TryGetValue(identity, out profile!);

    private static ResidentPlayerProfiles Empty() =>
        new(new Dictionary<string, ResidentPlayerProfile>(StringComparer.Ordinal));

    private static JObject ParseObject(string json, string description)
    {
        try
        {
            using var stringReader = new StringReader(json);
            using var jsonReader = new JsonTextReader(stringReader)
            {
                DateParseHandling = DateParseHandling.None,
                MaxDepth = 16
            };
            JObject root = JObject.Load(jsonReader, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            if (jsonReader.Read())
            {
                throw new FormatException($"{description} has trailing content.");
            }

            return root;
        }
        catch (JsonException exception)
        {
            throw new FormatException($"{description} JSON is invalid.", exception);
        }
    }

    private static bool IsValidIdentityKey(string identity) =>
        identity.Length > 0
        && identity.Length <= MaximumIdentityCharacters
        && !ContainsUnsupportedCharacter(identity, allowNewlines: false, rejectMarkup: false);

    private static bool TryOptionalString(
        JObject profile,
        string fieldName,
        int maximumCharacters,
        bool allowNewlines,
        out string? value)
    {
        JProperty? property = profile.Property(fieldName, StringComparison.Ordinal);
        if (property == null)
        {
            value = null;
            return true;
        }

        if (property.Value.Type != JTokenType.String)
        {
            value = null;
            return false;
        }

        string text = property.Value.Value<string>()!.Trim();
        if (text.Length == 0)
        {
            value = null;
            return true;
        }

        if (text.Length > maximumCharacters
            || ContainsUnsupportedCharacter(text, allowNewlines, rejectMarkup: true))
        {
            value = null;
            return false;
        }

        value = text;
        return true;
    }

    private static bool ContainsUnsupportedCharacter(string value, bool allowNewlines, bool rejectMarkup)
    {
        foreach (char character in value)
        {
            if ((char.IsControl(character)
                    && !(allowNewlines && (character == '\r' || character == '\n')))
                || character == '\u007f'
                || (rejectMarkup && (character == '<' || character == '>')))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryInteger(JToken? token, out int value)
    {
        if (token?.Type == JTokenType.Integer)
        {
            try
            {
                long number = token.Value<long>();
                if (number >= int.MinValue && number <= int.MaxValue)
                {
                    value = (int)number;
                    return true;
                }
            }
            catch (Exception exception) when (exception is FormatException or OverflowException)
            {
                // A JSON integer outside Int64 is an invalid schema version.
            }
        }

        value = 0;
        return false;
    }

    private static bool HasAllowedFields(JObject value, IReadOnlyList<string> allowedFields, bool exact)
    {
        if (exact && value.Properties().Count() != allowedFields.Count)
        {
            return false;
        }

        foreach (JProperty property in value.Properties())
        {
            bool found = false;
            for (int index = 0; index < allowedFields.Count; index++)
            {
                if (string.Equals(property.Name, allowedFields[index], StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class ResidentPlayerProfile
{
    internal ResidentPlayerProfile(string? preferredName, string? gender, string? pronouns, string? lore)
    {
        PreferredName = preferredName;
        Gender = gender;
        Pronouns = pronouns;
        Lore = lore;
    }

    internal string? PreferredName { get; }
    internal string? Gender { get; }
    internal string? Pronouns { get; }
    internal string? Lore { get; }
}
