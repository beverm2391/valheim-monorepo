using System;
using System.Collections.Generic;

namespace BenheimInventoryProtocol
{
    internal enum DepositStatus { Success }

    internal sealed class DepositCandidate
    {
        internal DepositCandidate(ItemDrop.ItemData sourceItem) => SourceItem = sourceItem;
        internal ItemDrop.ItemData SourceItem { get; }
    }

    internal sealed class DepositResultEntry
    {
        internal DepositResultEntry(ItemDrop.ItemData item, int accepted) { Item = item; Accepted = accepted; }
        internal ItemDrop.ItemData Item { get; }
        internal int Accepted { get; }
    }

    internal sealed class DepositResult
    {
        internal DepositResult(DepositStatus status, List<DepositResultEntry> entries) { Status = status; Entries = entries; }
        internal DepositStatus Status { get; }
        internal List<DepositResultEntry> Entries { get; }
        internal bool Succeeded => Status == DepositStatus.Success;
    }

    internal static class InventoryTransactions
    {
        private static readonly Queue<Action<DepositResult>> pendingDeposits = new Queue<Action<DepositResult>>();
        private static bool hasUnsettledClientDeposit;

        internal static readonly List<BatchTerminal> Terminals = new List<BatchTerminal>();
        internal static readonly List<string> StartedOperationIds = new List<string>();
        internal static int BeginDepositCount { get; private set; }
        internal static int SettlementCount { get; private set; }
        internal static int ShutdownCount { get; private set; }
        internal static bool? UnsettledAtShutdown { get; private set; }
        internal static List<InventoryTransactionDiagnosticEvent> DiagnosticEvents { get; } =
            new List<InventoryTransactionDiagnosticEvent>();

        internal static bool HasUnsettledClientDeposit => hasUnsettledClientDeposit;
        internal static void SetUnsettledForTest(bool value) => hasUnsettledClientDeposit = value;

        internal static void BatchStarted(string operationId) => StartedOperationIds.Add(operationId);

        internal static void BatchFinished(
            string operationId,
            string status,
            string reason,
            int acceptedCount,
            double batchDurationMs,
            double scanMatchDurationMs) =>
            Terminals.Add(new BatchTerminal(operationId, status, reason));

        internal static bool TryBeginDeposit(
            string operationId,
            Player player,
            Container container,
            List<DepositCandidate> candidates,
            Action<DepositResult> callback)
        {
            BeginDepositCount++;
            hasUnsettledClientDeposit = true;
            pendingDeposits.Enqueue(callback);
            HarnessEvents.Events.Add("deposit-began");
            return true;
        }

        internal static void SettleNextDeposit()
        {
            if (pendingDeposits.Count == 0)
            {
                throw new InvalidOperationException("No deposit is waiting for settlement.");
            }

            Action<DepositResult> callback = pendingDeposits.Dequeue();
            hasUnsettledClientDeposit = pendingDeposits.Count > 0;
            SettlementCount++;
            HarnessEvents.Events.Add("deposit-settled");
            callback(new DepositResult(DepositStatus.Success, new List<DepositResultEntry>()));
        }

        internal static void Emit(InventoryTransactionDiagnosticEvent diagnosticEvent) => DiagnosticEvents.Add(diagnosticEvent);
        internal static void Initialize(IInventoryTransactionDiagnosticSink sink, string productVersion) { }
        internal static void Update() { }

        internal static void Shutdown()
        {
            ShutdownCount++;
            UnsettledAtShutdown = hasUnsettledClientDeposit;
            HarnessEvents.Events.Add("protocol-shutdown");
            hasUnsettledClientDeposit = false;
        }

        internal static void ResetForTest()
        {
            pendingDeposits.Clear();
            hasUnsettledClientDeposit = false;
            Terminals.Clear();
            StartedOperationIds.Clear();
            DiagnosticEvents.Clear();
            BeginDepositCount = 0;
            SettlementCount = 0;
            ShutdownCount = 0;
            UnsettledAtShutdown = null;
        }
    }

    internal sealed class BatchTerminal
    {
        internal BatchTerminal(string operationId, string status, string reason)
        {
            OperationId = operationId;
            Status = status;
            Reason = reason;
        }

        internal string OperationId { get; }
        internal string Status { get; }
        internal string Reason { get; }
    }
}
