using System;
using System.Collections.Generic;
using System.Linq;

namespace BenheimInventoryProtocol;

internal sealed class TransactionReceipt
{
    internal TransactionReceipt(int status, List<int> accepted)
    {
        Status = status;
        Accepted = accepted;
    }

    internal int Status { get; }
    internal List<int> Accepted { get; }
}

internal static class InventoryTransactionReceiptCodec
{
    internal const int MaxReceipts = 128;

    internal static bool TryRead(
        string encodedLedger,
        string transactionId,
        string payloadHash,
        out bool conflict,
        out TransactionReceipt? receipt)
    {
        conflict = false;
        receipt = null;
        foreach (string encoded in Split(encodedLedger))
        {
            string[] parts = encoded.Split('|');
            if (parts.Length != 4 || parts[0] != transactionId)
            {
                continue;
            }

            if (parts[1] != payloadHash)
            {
                conflict = true;
                return true;
            }

            if (!int.TryParse(parts[2], out int status))
            {
                return false;
            }

            List<int> accepted = new List<int>();
            if (parts[3].Length > 0)
            {
                foreach (string rawAmount in parts[3].Split(','))
                {
                    if (!int.TryParse(rawAmount, out int amount))
                    {
                        return false;
                    }

                    accepted.Add(amount);
                }
            }

            receipt = new TransactionReceipt(status, accepted);
            return true;
        }

        return false;
    }

    internal static string Record(
        string encodedLedger,
        string transactionId,
        string payloadHash,
        int status,
        IReadOnlyList<int> accepted)
    {
        if (!CanRecord(encodedLedger, transactionId))
        {
            throw new InvalidOperationException("Receipt ledger is full.");
        }

        List<string> entries = Split(encodedLedger);
        entries.RemoveAll(entry => entry.StartsWith(transactionId + "|", StringComparison.Ordinal));
        entries.Insert(
            0,
            string.Join(
                "|",
                transactionId,
                payloadHash,
                status.ToString(),
                string.Join(",", accepted)));
        return string.Join(";", entries);
    }

    internal static bool CanRecord(string encodedLedger, string transactionId)
    {
        List<string> entries = Split(encodedLedger);
        return entries.Any(entry => entry.StartsWith(transactionId + "|", StringComparison.Ordinal))
            || entries.Count < MaxReceipts;
    }

    internal static string Remove(string encodedLedger, string transactionId, string payloadHash)
    {
        List<string> entries = Split(encodedLedger);
        entries.RemoveAll(entry => entry.StartsWith(
            transactionId + "|" + payloadHash + "|",
            StringComparison.Ordinal));
        return string.Join(";", entries);
    }

    private static List<string> Split(string encodedLedger)
    {
        return string.IsNullOrEmpty(encodedLedger)
            ? new List<string>()
            : encodedLedger.Split(';').Where(entry => entry.Length > 0).ToList();
    }
}
