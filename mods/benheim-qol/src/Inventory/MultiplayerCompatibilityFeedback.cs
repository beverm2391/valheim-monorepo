using BenheimQoL.Infrastructure;
using BenheimInventoryProtocol;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class MultiplayerCompatibilityFeedback
{
    private const float HandshakeGraceSeconds = 8f;
    private const float EvaluationIntervalSeconds = 0.5f;
    private static readonly InventoryCompatibilityWarningTracker WarningTracker = new();
    private static InventoryCapabilitySnapshot? lastSnapshot;
    private static float nextEvaluationAt;

    internal static void Update()
    {
        InventoryCapabilitySnapshot snapshot = InventoryTransactions.GetCapabilitySnapshot();
        float now = Time.realtimeSinceStartup;
        if (ReferenceEquals(snapshot, lastSnapshot) && now < nextEvaluationAt)
        {
            return;
        }

        lastSnapshot = snapshot;
        nextEvaluationAt = now + EvaluationIntervalSeconds;
        if (!WarningTracker.TryGetWarningKey(
            snapshot,
            InventoryTransactions.ProtocolVersion,
            now,
            HandshakeGraceSeconds,
            out string warningKey))
        {
            return;
        }

        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            return;
        }

        player.Message(
            MessageHud.MessageType.Center,
            "Put Away is unavailable because multiplayer Benheim support does not match. Press F8 for details.");
        WarningTracker.MarkWarned(warningKey);
        Diagnostics.Event("Inventory", "compatibility_warning", $"roster={warningKey}");
    }
}
