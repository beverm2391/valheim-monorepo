using System.Collections.Generic;
using HarmonyLib;

namespace BenheimQoL.Infrastructure;

/// <summary>
/// Observes Valheim's complete account-to-character roster on clients and
/// local hosts. Platform identity is supplied by the native authenticated
/// player list; character names are labels and can change independently.
/// </summary>
[HarmonyPatch]
internal static class PlayerIdentityDiagnostics
{
    private static readonly PlayerIdentitySnapshot Snapshot = new PlayerIdentitySnapshot();

    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    [HarmonyPrefix]
    internal static void BeforeNewConnection(ZNet __instance, ZNetPeer peer)
    {
        // A client may connect to a different world while this plugin remains
        // loaded. Do not carry the prior server's roster into that session.
        if (!__instance.IsServer() && peer.m_server)
        {
            Snapshot.Reset();
        }
    }

    [HarmonyPatch(typeof(ZNet), "RPC_PlayerList")]
    [HarmonyPostfix]
    internal static void AfterClientPlayerList(ZNet __instance)
    {
        if (ZNet.instance == __instance && !__instance.IsServer())
        {
            ObserveCurrentRoster(__instance);
        }
    }

    [HarmonyPatch(typeof(ZNet), "SendPlayerList")]
    [HarmonyPostfix]
    internal static void AfterLocalHostPlayerList(ZNet __instance)
    {
        // A listen/local host constructs its own list rather than receiving
        // RPC_PlayerList. Dedicated servers do not run this client assembly.
        if (ZNet.instance == __instance && __instance.IsServer())
        {
            ObserveCurrentRoster(__instance);
        }
    }

    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    [HarmonyPrefix]
    internal static void BeforeNetworkDestroy()
    {
        Snapshot.Reset();
    }

    internal static void ObserveCurrentRoster(ZNet network)
    {
        List<PlayerIdentityObservation> observations = new List<PlayerIdentityObservation>();
        foreach (ZNet.PlayerInfo player in network.GetPlayerList())
        {
            if (!player.m_userInfo.m_id.IsValid)
            {
                // Some cross-platform backends do not expose a stable native
                // account ID. Do not substitute a session ID or character ID.
                continue;
            }

            observations.Add(new PlayerIdentityObservation(
                player.m_userInfo.m_id.ToString(),
                player.m_name ?? string.Empty));
        }

        foreach (PlayerIdentityChange change in Snapshot.Observe(observations))
        {
            Diagnostics.Emit(DiagnosticEvent.Create("PlayerIdentity", "observed")
                .String("change_kind", change.ChangeKind)
                .String("platform_user_id", change.PlatformUserId)
                .String("character_name", change.CharacterName));
        }
    }
}
