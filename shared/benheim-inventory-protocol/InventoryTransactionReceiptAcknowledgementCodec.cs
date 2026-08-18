using System;

namespace BenheimInventoryProtocol;

/// <summary>
/// Owns the one-way requester-to-owner receipt-cleanup package shape.
/// </summary>
internal static class InventoryTransactionReceiptAcknowledgementCodec
{
    internal static ZPackage Write(
        string transactionId,
        string payloadHash,
        ZDOID containerId)
    {
        ZPackage package = new ZPackage();
        package.Write(transactionId);
        package.Write(payloadHash);
        package.Write(containerId);
        return package;
    }

    internal static bool TryRead(
        ZPackage package,
        out string transactionId,
        out string payloadHash,
        out ZDOID containerId)
    {
        transactionId = string.Empty;
        payloadHash = string.Empty;
        containerId = ZDOID.None;
        try
        {
            transactionId = package.ReadString();
            payloadHash = package.ReadString();
            containerId = package.ReadZDOID();
            if (string.IsNullOrEmpty(transactionId)
                || string.IsNullOrEmpty(payloadHash)
                || containerId.IsNone()
                || package.GetPos() != package.Size())
            {
                return false;
            }

            return true;
        }
        catch (Exception)
        {
            transactionId = string.Empty;
            payloadHash = string.Empty;
            containerId = ZDOID.None;
            return false;
        }
    }

    internal static bool TryAuthorize(
        ZPackage package,
        ConnectedTransactionRouter<ZDOID> router,
        long routedSender,
        out string transactionId,
        out string payloadHash,
        out ZDOID containerId,
        out string rejectionReason)
    {
        if (!TryRead(package, out transactionId, out payloadHash, out containerId))
        {
            rejectionReason = "malformed_ack";
            return false;
        }

        if (!router.MatchesCompleted(
                transactionId,
                routedSender,
                payloadHash,
                containerId))
        {
            rejectionReason = "completed_correlation_mismatch";
            return false;
        }

        rejectionReason = string.Empty;
        return true;
    }
}
