using System;

namespace BenheimQoL.InventoryFeature;

/// <summary>
/// Connects serial cohort validation to pipelined deposit transactions. The
/// caller supplies the real lease and transaction transports; this class owns
/// their ordering and the single drain-before-release terminal callback.
/// </summary>
internal sealed class QuickStackBatchPipeline<TResult>
{
    private readonly QuickStackBatchScheduler scheduler = new QuickStackBatchScheduler();
    private readonly Action<QuickStackBatchTerminal> terminalReady;
    private bool validationPending;

    internal QuickStackBatchPipeline(Action<QuickStackBatchTerminal> terminalReady)
    {
        this.terminalReady = terminalReady;
    }

    internal int InFlightCount => scheduler.InFlightCount;

    internal bool TryRequestValidation(Func<bool> requestValidation)
    {
        if (scheduler.SchedulingStopped || validationPending)
        {
            return false;
        }

        validationPending = true;
        if (requestValidation())
        {
            return true;
        }

        validationPending = false;
        return false;
    }

    internal bool TryBeginValidatedDeposit(
        Func<Action<TResult>, bool> beginDeposit,
        Action<TResult> settleDeposit,
        Action depositRejected,
        Action duplicateSettlement,
        Action<Exception> settlementFailed)
    {
        if (!validationPending || scheduler.SchedulingStopped)
        {
            return false;
        }

        validationPending = false;
        if (!scheduler.TryBeginDeposit(out int ticket))
        {
            return false;
        }

        if (beginDeposit(result => SettleDeposit(
                ticket,
                result,
                settleDeposit,
                duplicateSettlement,
                settlementFailed)))
        {
            return true;
        }

        scheduler.TrySettleDeposit(ticket, depositRejected);
        FinishIfReady();
        return true;
    }

    internal void StopScheduling(string status, string reason)
    {
        validationPending = false;
        scheduler.StopScheduling(status, reason);
        FinishIfReady();
    }

    private void SettleDeposit(
        int ticket,
        TResult result,
        Action<TResult> settleDeposit,
        Action duplicateSettlement,
        Action<Exception> settlementFailed)
    {
        try
        {
            if (!scheduler.TrySettleDeposit(ticket, () => settleDeposit(result)))
            {
                duplicateSettlement();
            }
        }
        catch (Exception exception)
        {
            settlementFailed(exception);
        }
        finally
        {
            FinishIfReady();
        }
    }

    private void FinishIfReady()
    {
        if (scheduler.TryTakeTerminal(out QuickStackBatchTerminal? terminal))
        {
            terminalReady(terminal!);
        }
    }
}
