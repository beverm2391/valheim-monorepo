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
        Action<Exception> settlementFailed,
        Action depositSettled)
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
                settlementFailed,
                depositSettled)))
        {
            return true;
        }

        try
        {
            scheduler.TrySettleDeposit(ticket, depositRejected);
        }
        catch (Exception exception)
        {
            settlementFailed(exception);
        }
        finally
        {
            depositSettled();
            FinishIfReady();
        }
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
        Action<Exception> settlementFailed,
        Action depositSettled)
    {
        bool firstSettlement = scheduler.IsInFlight(ticket);
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
            if (firstSettlement)
            {
                depositSettled();
            }
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

/// <summary>
/// Owns the one continuation decision after a validated deposit begins. A
/// dependency barrier waits for settlement or refund; an independent deposit
/// advances immediately. Synchronous rejection and duplicate delivery cannot
/// skip or repeat the continuation.
/// </summary>
internal sealed class QuickStackDepositContinuation
{
    private readonly bool waitForSettlement;
    private readonly Action continueScheduling;
    private bool beginCompleted;
    private bool settledDuringBegin;
    private bool continued;

    internal QuickStackDepositContinuation(
        bool waitForSettlement,
        Action continueScheduling)
    {
        this.waitForSettlement = waitForSettlement;
        this.continueScheduling = continueScheduling;
    }

    internal void DepositSettled()
    {
        if (!waitForSettlement || continued)
        {
            return;
        }

        if (!beginCompleted)
        {
            settledDuringBegin = true;
            return;
        }

        ContinueOnce();
    }

    internal void CompleteBegin(bool began)
    {
        if (beginCompleted)
        {
            throw new InvalidOperationException("Deposit continuation begin completed twice.");
        }

        beginCompleted = true;
        if (!began)
        {
            return;
        }

        if (!waitForSettlement || settledDuringBegin)
        {
            ContinueOnce();
        }
    }

    private void ContinueOnce()
    {
        if (continued)
        {
            return;
        }

        continued = true;
        continueScheduling();
    }
}
