using System;
using System.Collections.Generic;

namespace BenheimInventoryProtocol;

internal static partial class InventoryTransactions
{
    internal const int ProtocolVersion = InventoryTransactionRecoveryPolicy.CurrentProtocolVersion;
    internal const int MaxItemsPerDeposit = 64;
    private static readonly Dictionary<string, PendingDeposit> ClientPending = new Dictionary<string, PendingDeposit>();
    private static readonly Dictionary<string, PendingDeposit> ClientCompleted = new Dictionary<string, PendingDeposit>();
    private static bool journalRecoveryAttempted;
    private static readonly List<byte[]> SentRequests = new List<byte[]>();
    private static readonly List<string> Warnings = new List<string>();

    internal static void TestReset()
    {
        ClientPending.Clear();
        ClientCompleted.Clear();
        SentRequests.Clear();
        Warnings.Clear();
        journalRecoveryAttempted = false;
    }

    internal static void TestRecover() => RecoverPendingJournals();
    internal static bool TestIsPending(string transactionId) => ClientPending.ContainsKey(transactionId);
    internal static bool TestIsCompleted(string transactionId) => ClientCompleted.ContainsKey(transactionId);
    internal static PendingDeposit TestPending(string transactionId) => ClientPending[transactionId];
    internal static IReadOnlyList<byte[]> TestSentRequests => SentRequests;
    internal static IReadOnlyList<string> TestWarnings => Warnings;

    private static void SendDepositRequest(PendingDeposit pending)
    {
        SentRequests.Add((byte[])pending.RequestBytes.Clone());
    }

    internal static void LogDiagnostic(string message)
    {
    }

    internal static void LogWarning(string message)
    {
        Warnings.Add(message);
    }
}
