using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BenheimServerSupport;

/// <summary>
/// Joins the private operator profile map to Valheim's current native roster
/// and projects only bounded, account-free context for the speech provider.
/// </summary>
internal static class ResidentPlayerContext
{
    private const int MaximumConnectedProfiles = 16;
    private const int MaximumOutputCharacters = 16 * 1024;
    private const int MaximumCharacterNameCharacters = 64;

    /// <summary>
    /// Return the current visitor and other connected profiles as bounded
    /// JSON. The visitor is matched to the roster by character ZDOID, then the
    /// profile is joined by authenticated native PlatformUserID. Missing
    /// identities never fall back to transport IDs.
    /// </summary>
    internal static string ProviderContextFor(ZNetPeer requester)
    {
        List<ZNet.PlayerInfo> roster = ServerRosterFor(requester);
        return BuildProviderContext(requester, roster, ResidentPlayerProfiles.LoadConfigured());
    }

    // A path overload makes operator-file behavior testable without changing
    // the production location or creating a profile file as a side effect.
    internal static string ProviderContextFor(ZNetPeer requester, string mapFilePath)
    {
        List<ZNet.PlayerInfo> roster = ServerRosterFor(requester);
        return BuildProviderContext(requester, roster, ResidentPlayerProfiles.Load(mapFilePath));
    }

    private static List<ZNet.PlayerInfo> ServerRosterFor(ZNetPeer requester)
    {
        if (requester == null)
        {
            throw new ArgumentNullException(nameof(requester));
        }

        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            throw new InvalidOperationException("Player context requires the active Valheim server roster.");
        }

        return ZNet.instance.GetPlayerList()
            ?? throw new InvalidOperationException("The active Valheim player roster is unavailable.");
    }

    private static string BuildProviderContext(
        ZNetPeer requester,
        IReadOnlyList<ZNet.PlayerInfo> roster,
        ResidentPlayerProfiles profiles)
    {
        int visitorIndex = FindVisitorRosterIndex(requester, roster);
        ZNet.PlayerInfo visitorInfo = visitorIndex >= 0 ? roster[visitorIndex] : default;
        string visitorName = SanitizeNativeName(
            visitorIndex >= 0 ? visitorInfo.m_name : requester.m_playerName);
        string? visitorIdentity = visitorIndex >= 0 ? NativeIdentity(visitorInfo) : null;

        JObject visitor = BuildProfileJson(visitorName, visitorIdentity, profiles);
        var connectedPlayers = new JArray();
        var emittedIdentities = new HashSet<string>(StringComparer.Ordinal);
        if (visitorIdentity != null)
        {
            emittedIdentities.Add(visitorIdentity);
        }

        for (int index = 0; index < roster.Count && connectedPlayers.Count < MaximumConnectedProfiles; index++)
        {
            if (index == visitorIndex)
            {
                continue;
            }

            ZNet.PlayerInfo player = roster[index];
            string? identity = NativeIdentity(player);
            if (identity != null && !emittedIdentities.Add(identity))
            {
                continue;
            }

            string name = SanitizeNativeName(player.m_name);
            JObject profile = BuildProfileJson(name, identity, profiles);
            if (name.Length == 0 && profile.Properties().Count() == 1)
            {
                // Skip roster rows with neither a usable character name nor
                // an authored profile.
                continue;
            }

            connectedPlayers.Add(profile);
        }

        string json = new JObject
        {
            ["visitor"] = visitor,
            ["knownConnectedPlayers"] = connectedPlayers
        }.ToString(Formatting.None);

        if (json.Length > MaximumOutputCharacters)
        {
            throw new InvalidOperationException("Player provider context exceeded its output limit.");
        }

        return json;
    }

    private static int FindVisitorRosterIndex(ZNetPeer requester, IReadOnlyList<ZNet.PlayerInfo> roster)
    {
        if (requester.m_characterID.IsNone())
        {
            return -1;
        }

        for (int index = 0; index < roster.Count; index++)
        {
            if (roster[index].m_characterID == requester.m_characterID)
            {
                return index;
            }
        }

        return -1;
    }

    private static string? NativeIdentity(ZNet.PlayerInfo player)
    {
        if (!player.m_userInfo.m_id.IsValid)
        {
            return null;
        }

        string identity = player.m_userInfo.m_id.ToString();
        return string.IsNullOrWhiteSpace(identity) ? null : identity;
    }

    private static JObject BuildProfileJson(
        string currentCharacterName,
        string? identity,
        ResidentPlayerProfiles profiles)
    {
        var json = new JObject
        {
            ["currentCharacterName"] = currentCharacterName
        };

        if (identity != null && profiles.TryGet(identity, out ResidentPlayerProfile profile))
        {
            AddIfPresent(json, "preferredName", profile.PreferredName);
            AddIfPresent(json, "gender", profile.Gender);
            AddIfPresent(json, "pronouns", profile.Pronouns);
            AddIfPresent(json, "lore", profile.Lore);
        }

        return json;
    }

    private static void AddIfPresent(JObject target, string name, string? value)
    {
        if (value != null)
        {
            target[name] = value;
        }
    }

    private static string SanitizeNativeName(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = new StringBuilder(Math.Min(value.Length, MaximumCharacterNameCharacters));
        bool insideMarkup = false;
        foreach (char character in value)
        {
            if (sanitized.Length == MaximumCharacterNameCharacters)
            {
                break;
            }

            if (insideMarkup)
            {
                insideMarkup = character != '>';
                continue;
            }

            if (character == '<')
            {
                insideMarkup = true;
                continue;
            }

            if (char.IsControl(character) || character == '\u007f' || character == '>')
            {
                continue;
            }

            sanitized.Append(character);
        }

        return sanitized.ToString().Trim();
    }
}
