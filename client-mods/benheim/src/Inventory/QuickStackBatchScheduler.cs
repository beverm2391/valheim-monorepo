using System;
using System.Collections.Generic;

namespace BenheimQoL.InventoryFeature;

/// <summary>
/// Owns only Put Away's local batch scheduling boundary. Each ticket represents
/// a transaction whose source items were reserved synchronously by the existing
/// owner-authoritative transaction API. The global lease can be released only
/// after scheduling stops and every issued ticket reaches one terminal.
/// </summary>
internal sealed class QuickStackBatchScheduler
{
    private readonly HashSet<int> inFlight = new HashSet<int>();
    private int nextTicket;
    private bool schedulingStopped;
    private bool terminalTaken;
    private string terminalStatus = string.Empty;
    private string terminalReason = string.Empty;

    internal int InFlightCount => inFlight.Count;
    internal bool SchedulingStopped => schedulingStopped;
    internal bool IsInFlight(int ticket) => inFlight.Contains(ticket);

    internal bool TryBeginDeposit(out int ticket)
    {
        ticket = 0;
        if (schedulingStopped)
        {
            return false;
        }

        ticket = ++nextTicket;
        inFlight.Add(ticket);
        return true;
    }

    internal bool TrySettleDeposit(int ticket, Action settlement)
    {
        if (!inFlight.Remove(ticket))
        {
            return false;
        }

        // Removing the ticket before invoking presentation/accounting makes a
        // duplicate callback harmless. The state stays terminal-ready even if
        // that callback throws; item settlement already completed inside the
        // transaction protocol before this scheduler callback runs.
        settlement();
        return true;
    }

    internal void StopScheduling(string status, string reason)
    {
        if (schedulingStopped)
        {
            return;
        }

        schedulingStopped = true;
        terminalStatus = status;
        terminalReason = reason;
    }

    internal bool TryTakeTerminal(out QuickStackBatchTerminal? terminal)
    {
        if (!schedulingStopped || inFlight.Count != 0 || terminalTaken)
        {
            terminal = null;
            return false;
        }

        terminalTaken = true;
        terminal = new QuickStackBatchTerminal(terminalStatus, terminalReason);
        return true;
    }
}

internal static class QuickStackBatchDependencies
{
    internal static bool HasItemNameOverlap(
        IReadOnlyCollection<string> candidateItemNames,
        IReadOnlyCollection<string> laterContainerItemNames)
    {
        HashSet<string> candidates = new HashSet<string>(candidateItemNames);
        foreach (string laterItemName in laterContainerItemNames)
        {
            if (candidates.Contains(laterItemName))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class QuickStackBatchTerminal
{
    internal QuickStackBatchTerminal(string status, string reason)
    {
        Status = status;
        Reason = reason;
    }

    internal string Status { get; }
    internal string Reason { get; }
    internal bool Completed => Status == "completed";
}
