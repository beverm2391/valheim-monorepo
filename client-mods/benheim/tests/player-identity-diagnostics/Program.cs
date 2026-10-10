using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;

string firstAccount = "test-platform-account-001";
string secondAccount = "test-platform-account-002";
ZNet client = new ZNet(isServer: false);
ZNet.instance = client;
client.Players.Add(Player(firstAccount, "TestOne"));
client.Players.Add(Player(secondAccount, "TestTwo"));

PlayerIdentityDiagnostics.AfterClientPlayerList(client);
Expect(Diagnostics.Events.Count == 2, "the first complete client roster emits one event per valid account");
ExpectEvent(0, firstAccount, "TestOne", "first_seen");
ExpectEvent(1, secondAccount, "TestTwo", "first_seen");

PlayerIdentityDiagnostics.AfterClientPlayerList(client);
PlayerIdentityDiagnostics.AfterClientPlayerList(client);
Expect(Diagnostics.Events.Count == 2, "periodic identical roster refreshes do not repeat identity events");

client.Players[1] = Player(secondAccount, "RenamedTwo");
PlayerIdentityDiagnostics.AfterClientPlayerList(client);
Expect(Diagnostics.Events.Count == 3, "a character rename emits one event");
ExpectEvent(2, secondAccount, "RenamedTwo", "character_name_changed");

client.Players.RemoveAt(0);
PlayerIdentityDiagnostics.AfterClientPlayerList(client);
Expect(Diagnostics.Events.Count == 3, "a departure only updates the snapshot");
client.Players.Insert(0, Player(firstAccount, "TestOne"));
PlayerIdentityDiagnostics.AfterClientPlayerList(client);
Expect(Diagnostics.Events.Count == 4, "a departed account is observed once when it rejoins");
ExpectEvent(3, firstAccount, "TestOne", "first_seen");

client.Players.Add(Player("", "NoStableAccount"));
client.Players.Add(Player("test-platform-account-invalid", "InvalidAccount", valid: false));
PlayerIdentityDiagnostics.AfterClientPlayerList(client);
Expect(Diagnostics.Events.Count == 4, "missing and invalid native account IDs are skipped");

PlayerIdentityDiagnostics.BeforeNewConnection(client, new ZNetPeer(isServer: true));
PlayerIdentityDiagnostics.AfterClientPlayerList(client);
Expect(Diagnostics.Events.Count == 6, "a new server connection starts a fresh observed roster");

ZNet host = new ZNet(isServer: true);
ZNet.instance = host;
host.Players.Add(Player("test-platform-host-account", "LocalHost"));
PlayerIdentityDiagnostics.AfterLocalHostPlayerList(host);
Expect(Diagnostics.Events.Count == 7, "a local host observes its native server-built roster");
ExpectEvent(6, "test-platform-host-account", "LocalHost", "first_seen");

PlayerIdentityDiagnostics.BeforeNetworkDestroy();
PlayerIdentityDiagnostics.AfterLocalHostPlayerList(host);
Expect(Diagnostics.Events.Count == 8, "network destruction clears the previous roster");

Console.WriteLine("player identity lifecycle and typed diagnostics checks passed");

static ZNet.PlayerInfo Player(string accountId, string name, bool valid = true)
{
    return new ZNet.PlayerInfo
    {
        m_name = name,
        m_userInfo = new CrossNetworkUserInfo
        {
            m_id = new PlatformUserID(accountId, valid)
        }
    };
}

static void ExpectEvent(int index, string accountId, string characterName, string changeKind)
{
    string json = Diagnostics.Events[index];
    Expect(json.Contains("\"platform_user_id\":\"" + accountId + "\"", StringComparison.Ordinal),
        "typed identity event contains its native account identity");
    Expect(json.Contains("\"character_name\":\"" + characterName + "\"", StringComparison.Ordinal),
        "typed identity event contains the current character name");
    Expect(json.Contains("\"change_kind\":\"" + changeKind + "\"", StringComparison.Ordinal),
        "typed identity event explains why it was emitted");
    Expect(!json.Contains("playfab", StringComparison.OrdinalIgnoreCase),
        "typed identity event excludes other provider identifiers");
}

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

namespace BenheimQoL.Infrastructure
{
    internal static class Diagnostics
    {
        internal static readonly List<string> Events = new List<string>();

        internal static void Emit(DiagnosticEvent diagnosticEvent)
        {
            diagnosticEvent.Prepare(DateTime.UtcNow, "test-session", "test-version");
            Events.Add(diagnosticEvent.ToJsonLine());
        }
    }
}
