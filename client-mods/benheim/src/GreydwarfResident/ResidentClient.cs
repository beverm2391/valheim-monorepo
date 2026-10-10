using System;
using System.Collections.Generic;
using System.Linq;
using Benheim.Resident;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.GreydwarfResident;

// Only the current server connection can authorize a shared encounter. The
// native owner writes preserve the ordinary tub; rendering is cosmetic.
[HarmonyPatch]
internal static class ResidentClient
{
    private static ZRpc? connection;
    private static bool compatible, cohortReady, warned;
    private static float nextCapability, connectedAt;
    private static readonly Dictionary<ZDOID, ResidentTubClient> tubs = new();
    private static readonly Dictionary<string, PendingEncounter> encounters = new();
    private static readonly Dictionary<string, float> cancelledEncounters = new();
    internal static bool Available => GreydwarfResidentRuntime.IsEnabled &&
        compatible && cohortReady && connection != null &&
         connection.IsConnected() && ReferenceEquals(connection, ZNet.instance?.GetServerRPC());

    [HarmonyPatch(typeof(ZNet), "OnNewConnection"), HarmonyPostfix]
    private static void Connected(ZNetPeer peer)
    {
        if (!ZNet.instance || ZNet.instance.IsServer() || !peer.m_server) return;
        ResetConnection();
        connection = peer.m_rpc;
        connectedAt = Time.realtimeSinceStartup;
        connection.Register<int, bool>(ResidentProtocol.CapabilityRpc, OnCapability);
        connection.Register<ZPackage>(ResidentProtocol.PlacementResultRpc, OnPlacement);
        connection.Register<ZPackage>(ResidentProtocol.EncounterGrantRpc, OnGrant);
        connection.Register<ZPackage>(ResidentProtocol.SpeechResultRpc, OnSpeech);
    }

    [HarmonyPatch(typeof(ZNet), "OnDestroy"), HarmonyPrefix]
    private static void NetworkDestroyed() => Reset();

    internal static void Update()
    {
        if (connection == null || !ReferenceEquals(connection, ZNet.instance?.GetServerRPC())) return;
        float now = Time.realtimeSinceStartup;
        if (now >= nextCapability && connection.IsConnected())
        {
            nextCapability = now + 1f;
            connection.Invoke(ResidentProtocol.CapabilityRpc, ResidentProtocol.Version);
        }
        if (!compatible && !warned && now - connectedAt >= 5f)
        {
            warned = true;
            Plugin.Log.LogWarning("George requires matching Benheim Server Support and compatible players.");
            ResidentDiagnostics.Emit("capability_unavailable", "server_timeout");
        }
        foreach (var pair in encounters.ToArray())
            if (!Relevant(pair.Value) || now >= pair.Value.Expires)
                Cancel(pair.Key, now >= pair.Value.Expires ? "expired" : "visitor_or_tub_unavailable");
        foreach (var pair in cancelledEncounters.Where(p => now >= p.Value).ToArray()) cancelledEncounters.Remove(pair.Key);
    }

    private static void OnCapability(ZRpc rpc, int version, bool ready)
    {
        if (!Current(rpc)) return;
        bool wasAvailable = Available;
        compatible = version == ResidentProtocol.Version;
        cohortReady = compatible && ready;
        if (wasAvailable != Available)
        {
            ResidentDiagnostics.Emit("capability_changed", Available ? "all_peers_ready" : "peer_support_missing");
            if (!Available)
            {
                Message("George is waiting for compatible Benheim players and Server Support.");
                foreach (string id in encounters.Keys.ToArray()) Cancel(id, "peer_support_missing");
            }
        }
    }

    internal static void Register(ResidentTubClient tub)
    {
        if (tub.View && tub.View.IsValid()) tubs[tub.View.GetZDO().m_uid] = tub;
    }

    internal static void Unregister(ResidentTubClient tub)
    {
        foreach (var pair in tubs.Where(p => p.Value == tub).ToArray()) tubs.Remove(pair.Key);
        foreach (var pair in encounters.Where(p => p.Value.Tub == tub).ToArray()) Cancel(pair.Key, "tub_unloaded");
    }

    internal static void Place(ZNetView view)
    {
        if (!Available)
        {
            Message("George needs matching Benheim clients and Server Support.");
            ResidentDiagnostics.Emit("placement_rejected", "support_unavailable");
            return;
        }
        Player player = Player.m_localPlayer;
        if (!player || !view.IsValid()) return;
        if (Vector3.Distance(player.transform.position, view.transform.position) > 6f)
        {
            Message("Move closer to the hot tub to invite or dismiss George.");
            ResidentDiagnostics.Emit("placement_rejected", "requester_out_of_range");
            return;
        }
        ZDO zdo = view.GetZDO();
        bool desired = !ResidentTub.IsInvited(zdo);
        string operation = Guid.NewGuid().ToString("N");
        ZPackage request = new();
        request.Write(operation); request.Write(zdo.m_uid); request.Write(desired);
        request.Write(ResidentTub.Generation(zdo));
        connection!.Invoke(ResidentProtocol.PlacementRequestRpc, request);
        ResidentDiagnostics.Operation("placement_requested", desired ? "invite" : "dismiss", operation, zdo.m_uid, ResidentTub.Generation(zdo));
    }

    private static void OnPlacement(ZRpc rpc, ZPackage package)
    {
        if (!Current(rpc)) return;
        try
        {
            string operation = package.ReadString(); ZDOID tub = package.ReadZDOID();
            bool success = package.ReadBool(); string reason = package.ReadString(); int generation = package.ReadInt();
            ResidentDiagnostics.Operation(success ? "placement_applied" : "placement_rejected", reason, operation, tub, generation);
            if (!success) Message("George: " + reason.Replace('_', ' '));
        }
        catch { ResidentDiagnostics.Emit("placement_rejected", "malformed_result"); }
    }

    internal static void Approach(ResidentTubClient tub, GreydwarfResidentBehaviour resident, Player player)
    {
        if (!Available || !tub.View || !tub.View.IsValid() || encounters.Values.Any(e => e.Tub == tub)) return;
        ZDO zdo = tub.View.GetZDO();
        string id = Guid.NewGuid().ToString("N");
        PendingEncounter pending = new(tub, resident, player, ResidentTub.Generation(zdo), Time.realtimeSinceStartup + 12f);
        encounters[id] = pending;
        ResidentDiagnostics.Operation("encounter_requested", "approach", id, zdo.m_uid, pending.Generation);
        ResidentSpeechTrace.Record(id, "approach", new { generation = pending.Generation });
        ZPackage request = new(); request.Write(id); request.Write(zdo.m_uid); request.Write(pending.Generation);
        connection!.Invoke(ResidentProtocol.EncounterRequestRpc, request);
    }

    private static void OnGrant(ZRpc rpc, ZPackage package)
    {
        if (!Current(rpc)) return;
        try
        {
            string id = package.ReadString(); ZDOID tub = package.ReadZDOID(); int generation = package.ReadInt();
            ZDOID visitor = package.ReadZDOID(); bool accepted = package.ReadBool(); string reason = package.ReadString();
            if (!encounters.TryGetValue(id, out var pending)) return;
            if (!accepted || !Relevant(pending) || pending.Tub.View.GetZDO().m_uid != tub ||
                pending.Generation != generation || pending.Player.GetZDOID() != visitor)
            { Cancel(id, accepted ? "stale_grant" : reason); return; }
            if (pending.Request != null) return;
            Begin(id, pending);
        }
        catch { ResidentDiagnostics.Emit("speech_rejected", "malformed_grant"); }
    }

    private static void Begin(string id, PendingEncounter pending)
    {
        pending.Request = ResidentSpeech.Begin(pending.Resident, id, pending.Player, pending.Tub.Station,
            pending.Tub.RecentRemarks.ToArray(), () => encounters.ContainsKey(id) && Relevant(pending),
            (speak, text, reason) => Complete(id, pending, speak, text, reason));
    }

    private static void Complete(string id, PendingEncounter pending, bool speak, string text, string reason)
    {
        if (!encounters.ContainsKey(id) || pending.ReplySent) return;
        if (!Relevant(pending)) { Cancel(id, "stale_reply"); return; }
        // Keep relevance live until the server's result arrives. Otherwise a
        // leave/return between HTTP completion and delivery could revive speech.
        pending.ReplySent = true;
        ZDOID tub = pending.Tub.View.GetZDO().m_uid;
        ZPackage reply = new(); reply.Write(id); reply.Write(tub); reply.Write(pending.Generation);
        reply.Write(speak); reply.Write(text); reply.Write(reason);
        connection!.Invoke(ResidentProtocol.SpeechReplyRpc, reply);
    }

    private static void OnSpeech(ZRpc rpc, ZPackage package)
    {
        if (!Current(rpc) || !Available) return;
        try
        {
            string id = package.ReadString(); ZDOID tub = package.ReadZDOID(); int generation = package.ReadInt();
            ZDOID visitor = package.ReadZDOID(); bool speak = package.ReadBool();
            string text = package.ReadString(); string reason = package.ReadString();
            if (cancelledEncounters.ContainsKey(id))
            { ResidentSpeechTrace.Record(id, "discarded", new { reason = "encounter_cancelled" }); return; }
            Display(id, tub, generation, visitor, speak, text, reason);
            encounters.Remove(id);
        }
        catch { ResidentDiagnostics.Emit("speech_rejected", "malformed_result"); }
    }

    private static void Display(string id, ZDOID tubId, int generation, ZDOID visitorId, bool speak, string text, string reason)
    {
        if (!tubs.TryGetValue(tubId, out var tub) || !tub || !tub.View.IsValid() ||
            ResidentTub.Generation(tub.View.GetZDO()) != generation || !ResidentTub.IsInvited(tub.View.GetZDO()) || !tub.Resident)
        { ResidentSpeechTrace.Record(id, "discarded", new { reason = "tub_or_generation_changed" }); return; }
        GameObject visitorObject = ZNetScene.instance ? ZNetScene.instance.FindInstance(visitorId) : null!;
        Player visitor = visitorObject ? visitorObject.GetComponent<Player>() : null!;
        if (!speak) { ResidentSpeechTrace.Record(id, "silence", new { reason }); return; }
        if (!visitor || !tub.Resident.CanSpeakTo(visitor) || !Chat.instance || text.Length == 0 || text.Length > 140 ||
            text.Any(c => c == '<' || c == '>' || char.IsControl(c)))
        { ResidentSpeechTrace.Record(id, "discarded", new { reason = "visitor_or_display_unavailable" }); return; }
        if (tub.LastSpeech == id) return;
        tub.LastSpeech = id;
        tub.Resident.ShowSpeech(visitor, text);
        tub.RecentRemarks.Add(text); if (tub.RecentRemarks.Count > 3) tub.RecentRemarks.RemoveAt(0);
        ResidentSpeechTrace.Record(id, "native_speech_submitted", new { text });
        ResidentDiagnostics.Operation("speech_displayed", "shared_result", id, tubId, generation);
    }

    private static bool Relevant(PendingEncounter pending) => Available && pending.Tub && pending.Tub.View &&
        pending.Tub.View.IsValid() && pending.Resident && pending.Player && pending.Player == Player.m_localPlayer &&
        ResidentTub.IsInvited(pending.Tub.View.GetZDO()) && ResidentTub.Generation(pending.Tub.View.GetZDO()) == pending.Generation &&
        pending.Resident.CanSpeakTo(pending.Player);

    private static void Cancel(string id, string reason)
    {
        if (!encounters.TryGetValue(id, out var pending)) return;
        encounters.Remove(id); pending.Request?.Dispose();
        cancelledEncounters[id] = Time.realtimeSinceStartup + 30f;
        ResidentSpeechTrace.Record(id, "discarded", new { reason });
        ResidentDiagnostics.Emit("speech_discarded", reason);
    }

    private static bool Current(ZRpc rpc) => ReferenceEquals(rpc, connection) && ReferenceEquals(rpc, ZNet.instance?.GetServerRPC());
    private static void Message(string message) { if (Player.m_localPlayer) Player.m_localPlayer.Message(MessageHud.MessageType.Center, message); }
    private static void ResetConnection()
    {
        foreach (string id in encounters.Keys.ToArray()) Cancel(id, "connection_reset");
        connection = null; compatible = cohortReady = warned = false; nextCapability = 0;
        cancelledEncounters.Clear();
    }
    internal static void Reset() { ResetConnection(); tubs.Clear(); }

    private sealed class PendingEncounter
    {
        internal readonly ResidentTubClient Tub;
        internal readonly GreydwarfResidentBehaviour Resident;
        internal readonly Player Player;
        internal readonly int Generation;
        internal readonly float Expires;
        internal IDisposable? Request;
        internal bool ReplySent;
        internal PendingEncounter(ResidentTubClient tub, GreydwarfResidentBehaviour resident, Player player, int generation, float expires)
        { Tub = tub; Resident = resident; Player = player; Generation = generation; Expires = expires; }
    }
}

[HarmonyPatch(typeof(Smelter), "Awake")]
internal static class ResidentTubAwakePatch
{
    private static void Postfix(Smelter __instance)
    {
        if (!ResidentTub.TryGet(__instance.gameObject, out ZNetView view)) return;
        ResidentTub.InstallOwnerHandler(view);
        if (!__instance.GetComponent<ResidentTubClient>())
            __instance.gameObject.AddComponent<ResidentTubClient>().Configure(__instance, view);
    }
}
