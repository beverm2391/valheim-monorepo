using System;
using System.Collections.Generic;
using BenheimInventoryProtocol;
using BenheimQoL.InventoryFeature;

PipelinesValidatedDepositsAndDrainsOutOfOrder();
ValidationTimeoutDrainsBeforeRelease();
PartialRefundConservesTheReservation();
CallbackExceptionAndDuplicateStillTerminateOnce();

Console.WriteLine("Put Away pipelined batch scheduler checks passed");

static void PipelinesValidatedDepositsAndDrainsOutOfOrder()
{
    TerminalProbe terminal = new TerminalProbe();
    FakeDepositTransport<int> transport = new FakeDepositTransport<int>();
    QuickStackBatchPipeline<int> pipeline = new QuickStackBatchPipeline<int>(terminal.Complete);
    int accepted = 0;

    StartValidatedDeposit(pipeline, transport, result => accepted += result);
    StartValidatedDeposit(pipeline, transport, result => accepted += result);
    Expect(transport.Pending.Count == 2 && pipeline.InFlightCount == 2,
        "the second deposit waited for the first owner result");

    Expect(pipeline.TryRequestValidation(() => true),
        "later cohort validation did not start with deposits in flight");
    pipeline.StopScheduling("cancelled", "peer_cohort_changed");
    Expect(!pipeline.TryRequestValidation(() => true),
        "failed cohort validation allowed another reservation");
    Expect(terminal.LeaseHeld && terminal.BatchTerminalCount == 0,
        "validation failure released before in-flight deposits settled");

    transport.Pending[1](2);
    Expect(accepted == 2 && terminal.LeaseHeld,
        "out-of-order result released with one deposit still in flight");
    transport.Pending[0](1);
    Expect(accepted == 3, "out-of-order results lost accepted counts");
    terminal.ExpectSingle("cancelled", "peer_cohort_changed");
}

static void ValidationTimeoutDrainsBeforeRelease()
{
    TerminalProbe terminal = new TerminalProbe();
    FakeDepositTransport<int> transport = new FakeDepositTransport<int>();
    QuickStackBatchPipeline<int> pipeline = new QuickStackBatchPipeline<int>(terminal.Complete);

    StartValidatedDeposit(pipeline, transport, _ => { });
    Expect(pipeline.TryRequestValidation(() => true),
        "timed-out cohort validation did not enter the pending state");
    pipeline.StopScheduling("cancelled", "server_no_response");
    Expect(terminal.LeaseHeld, "validation timeout released with a deposit in flight");

    transport.Pending[0](0);
    terminal.ExpectSingle("cancelled", "server_no_response");
}

static void PartialRefundConservesTheReservation()
{
    TerminalProbe terminal = new TerminalProbe();
    FakeDepositTransport<int> transport = new FakeDepositTransport<int>();
    QuickStackBatchPipeline<int> pipeline = new QuickStackBatchPipeline<int>(terminal.Complete);
    int accepted = 0;
    int refunded = 0;

    StartValidatedDeposit(pipeline, transport, reportedAccepted =>
    {
        Expect(InventoryTransactionSettlement.TryCreate(
                new[] { 7 },
                new[] { reportedAccepted },
                out InventoryTransactionSettlement? settlement),
            "partial owner result did not create an exact settlement");
        accepted = settlement!.Accepted[0];
        refunded = settlement.Rejected[0];
    });
    pipeline.StopScheduling("completed", "batch_finished");
    transport.Pending[0](3);

    Expect(accepted == 3 && refunded == 4 && accepted + refunded == 7,
        "partial settlement did not conserve the reserved count");
    terminal.ExpectSingle("completed", "batch_finished");
}

static void CallbackExceptionAndDuplicateStillTerminateOnce()
{
    TerminalProbe terminal = new TerminalProbe();
    FakeDepositTransport<int> transport = new FakeDepositTransport<int>();
    QuickStackBatchPipeline<int> pipeline = new QuickStackBatchPipeline<int>(terminal.Complete);
    int callbackFailures = 0;
    int duplicateCallbacks = 0;

    Expect(pipeline.TryRequestValidation(() => true), "throwing callback validation did not start");
    Expect(pipeline.TryBeginValidatedDeposit(
            transport.Begin,
            _ => throw new InvalidOperationException("presentation failed"),
            () => throw new InvalidOperationException("deposit unexpectedly rejected"),
            () => duplicateCallbacks++,
            _ => callbackFailures++),
        "throwing callback deposit did not start");
    pipeline.StopScheduling("completed", "batch_finished");

    transport.Pending[0](1);
    Expect(callbackFailures == 1, "throwing callback did not reach the failure boundary");
    terminal.ExpectSingle("completed", "batch_finished");

    transport.Pending[0](1);
    Expect(duplicateCallbacks == 1, "duplicate callback was not rejected");
    terminal.ExpectSingle("completed", "batch_finished");
}

static void StartValidatedDeposit(
    QuickStackBatchPipeline<int> pipeline,
    FakeDepositTransport<int> transport,
    Action<int> settle)
{
    Expect(pipeline.TryRequestValidation(() => true), "cohort validation did not start");
    Expect(pipeline.TryBeginValidatedDeposit(
            transport.Begin,
            settle,
            () => throw new InvalidOperationException("deposit unexpectedly rejected"),
            () => throw new InvalidOperationException("deposit unexpectedly settled twice"),
            exception => throw new InvalidOperationException("deposit callback failed", exception)),
        "validated deposit did not start");
}

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class FakeDepositTransport<TResult>
{
    internal List<Action<TResult>> Pending { get; } = new List<Action<TResult>>();

    internal bool Begin(Action<TResult> callback)
    {
        Pending.Add(callback);
        return true;
    }
}

internal sealed class TerminalProbe
{
    private QuickStackBatchTerminal? terminal;

    internal bool LeaseHeld { get; private set; } = true;
    internal int LeaseReleaseCount { get; private set; }
    internal int BatchTerminalCount { get; private set; }

    internal void Complete(QuickStackBatchTerminal completed)
    {
        LeaseHeld = false;
        LeaseReleaseCount++;
        BatchTerminalCount++;
        terminal = completed;
    }

    internal void ExpectSingle(string status, string reason)
    {
        Expect(!LeaseHeld, "the fake global lease was not released");
        Expect(LeaseReleaseCount == 1, "the fake global lease was released more than once");
        Expect(BatchTerminalCount == 1, "the batch terminal was emitted more than once");
        Expect(terminal != null && terminal.Status == status && terminal.Reason == reason,
            "the batch emitted the wrong terminal");
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
