using BenheimQoL.Infrastructure;

namespace BenheimServerSupport;

internal static class KillAttributionDiagnostics
{
    internal static void EmitCapability(
        ZNetPeer peer,
        string phase,
        string status,
        string reason,
        int requestedVersion,
        int serverVersion)
    {
        ServerDiagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "kill_feed_capability")
                .String("operation_phase", phase)
                .String("status", status)
                .String("reason", reason)
                .Integer("requested_protocol_version", requestedVersion)
                .Integer("server_protocol_version", serverVersion)
                .Integer("peer_uid", peer.m_uid)
                .String(
                    "character_id",
                    peer.m_characterID.IsNone() ? string.Empty : peer.m_characterID.ToString()));
    }

    internal static void EmitRejected(
        string reason,
        string operationId,
        ZDOID victimId,
        ZDOID killerId)
    {
        ServerDiagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "kill_report_rejected")
                .String("operation_id", operationId)
                .String("operation_phase", "validation")
                .String("status", "rejected")
                .String("reason", reason)
                .String("victim_id", victimId.IsNone() ? string.Empty : victimId.ToString())
                .String("killer_id", killerId.IsNone() ? string.Empty : killerId.ToString()));
    }
}
