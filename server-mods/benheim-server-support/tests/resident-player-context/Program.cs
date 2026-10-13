using BenheimServerSupport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

string directory = Path.Combine(Path.GetTempPath(), "resident-player-context-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    string mappedPath = Path.Combine(directory, "mapped.json");
    File.WriteAllText(mappedPath, """
        {
          "schemaVersion": 1,
          "players": {
            "Steam_visitor": {"preferredName":"Ben","gender":"male","pronouns":"he/him","lore":"Builds the mead hall."},
            "Steam_other": {"preferredName":"Tor","lore":"Keeps the forge lit."},
            "1001": {"preferredName":"Transport identity must not match"}
          }
        }
        """);

    ZDOID visitorId = new(1, 1);
    ZNet.instance = new ZNet(new List<ZNet.PlayerInfo>
    {
        Player("Einar", visitorId, "Steam_visitor"),
        Player("Sigríðr", new ZDOID(2, 1), "Steam_other"),
        Player("Unnamed", new ZDOID(3, 1), "")
    });
    var requester = new ZNetPeer
    {
        m_uid = 1001,
        m_characterID = visitorId,
        m_playerName = "stale peer label"
    };

    string json = ResidentPlayerContext.ProviderContextFor(requester, mappedPath);
    JObject context = JObject.Parse(json);
    JObject visitor = (JObject)context["visitor"]!;
    Expect(visitor["currentCharacterName"]!.Value<string>() == "Einar", "visitor uses the current native roster name");
    Expect(visitor["preferredName"]!.Value<string>() == "Ben", "visitor profile joins by native account identity");
    Expect(visitor["gender"]!.Value<string>() == "male" && visitor["pronouns"]!.Value<string>() == "he/him",
        "only explicitly authored gender and pronouns are included");
    Expect(visitor["lore"]!.Value<string>() == "Builds the mead hall.", "authored visitor lore is included");
    JArray connected = (JArray)context["knownConnectedPlayers"]!;
    Expect(connected.Count == 2, "visitor is excluded from the other connected players list");
    JObject other = (JObject)connected[0]!;
    Expect(other["currentCharacterName"]!.Value<string>() == "Sigríðr" && other["preferredName"]!.Value<string>() == "Tor",
        "connected mapped profile is joined by its native account identity");
    JObject unmapped = (JObject)connected[1]!;
    Expect(unmapped.Properties().Select(property => property.Name).SequenceEqual(new[] { "currentCharacterName" }),
        "unmapped connected player receives only the current character name");
    Expect(!json.Contains("Steam_visitor", StringComparison.Ordinal)
        && !json.Contains("Steam_other", StringComparison.Ordinal)
        && !json.Contains("1001", StringComparison.Ordinal)
        && !json.Contains("Transport identity", StringComparison.Ordinal),
        "provider context contains neither raw account IDs nor the transport peer ID");

    string missingMapPath = Path.Combine(directory, "missing.json");
    string fallbackJson = ResidentPlayerContext.ProviderContextFor(requester, missingMapPath);
    JObject fallback = JObject.Parse(fallbackJson);
    JObject fallbackVisitor = (JObject)fallback["visitor"]!;
    Expect(fallbackVisitor.Properties().Select(property => property.Name).SequenceEqual(new[] { "currentCharacterName" })
        && fallbackVisitor["currentCharacterName"]!.Value<string>() == "Einar",
        "missing profile map falls back to the current character name without inference");

    ZDOID markedNameId = new(4, 1);
    ZNet.instance = new ZNet(new List<ZNet.PlayerInfo>
    {
        Player("<color=red>Runa</color>\n", markedNameId, "Steam_marked")
    });
    JObject sanitized = JObject.Parse(ResidentPlayerContext.ProviderContextFor(new ZNetPeer
    {
        m_characterID = markedNameId,
        m_playerName = "unused"
    }, missingMapPath));
    Expect(sanitized["visitor"]!["currentCharacterName"]!.Value<string>() == "Runa",
        "native character markup and controls are removed from provider context");

    File.WriteAllText(mappedPath, "{not-json");
    ExpectThrows<FormatException>(() => ResidentPlayerContext.ProviderContextFor(requester, mappedPath),
        "malformed profile JSON remains visible as a failure");
    File.WriteAllText(mappedPath, "{\"schemaVersion\":1,\"players\":{},\"other\":true}");
    ExpectThrows<FormatException>(() => ResidentPlayerContext.ProviderContextFor(requester, mappedPath),
        "unexpected map fields fail visibly");
    File.WriteAllText(mappedPath, "{\"schemaVersion\":1,\"players\":{\"Steam_visitor\":{\"gender\":\"male\",\"gender\":\"female\"}}}");
    ExpectThrows<FormatException>(() => ResidentPlayerContext.ProviderContextFor(requester, mappedPath),
        "duplicate profile fields fail visibly");

    string boundedPath = Path.Combine(directory, "bounded.json");
    var boundedProfiles = new Dictionary<string, object>();
    for (int index = 0; index < 30; index++)
    {
        boundedProfiles["account_" + index] = new { lore = new string('l', 512) };
    }
    File.WriteAllText(boundedPath, JsonConvert.SerializeObject(new { schemaVersion = 1, players = boundedProfiles }));
    ZNet.instance = new ZNet(Enumerable.Range(0, 31)
        .Select(index => Player("Player " + index, new ZDOID(index + 1, 1), "account_" + index))
        .ToList());
    string boundedJson = ResidentPlayerContext.ProviderContextFor(new ZNetPeer
    {
        m_uid = 9999,
        m_characterID = new ZDOID(1, 1),
        m_playerName = "Player 0"
    }, boundedPath);
    JObject boundedContext = JObject.Parse(boundedJson);
    Expect(((JArray)boundedContext["knownConnectedPlayers"]!).Count == 16,
        "connected profile count has a fixed maximum");
    Expect(boundedJson.Length <= 16 * 1024, "serialized provider context stays within its character limit");

    Console.WriteLine("Resident player context profile checks passed");
}
finally
{
    ZNet.instance = null;
    Directory.Delete(directory, recursive: true);
}

static ZNet.PlayerInfo Player(string name, ZDOID characterId, string accountId) => new()
{
    m_name = name,
    m_characterID = characterId,
    m_userInfo = new CrossNetworkUserInfo { m_id = new PlatformUserID(accountId) }
};

static void Expect(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException("Failed: " + description);
    }
}

static void ExpectThrows<TException>(Action action, string description)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException("Expected " + typeof(TException).Name + ": " + description);
}
