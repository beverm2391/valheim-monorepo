using System;
using System.Collections.Generic;
using BenheimInventoryProtocol;
using BenheimQoL.InventoryFeature;

PipelinesValidatedDepositsAndDrainsOutOfOrder();
EagerSameItemSchedulingStrandsTheRefundControl();
WaitsForDependentRefundAndPipelinesDisjointItems();
ContinuationHandlesSynchronousRejectionFailureAndDuplicate();
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

static void EagerSameItemSchedulingStrandsTheRefundControl()
{
    int playerWood = 100;
    int reservedWood = playerWood;
    playerWood = 0;
    bool fartherChestSawCandidate = playerWood > 0;
    int nearestAccepted = 1;
    playerWood += reservedWood - nearestAccepted;

    Expect(!fartherChestSawCandidate && playerWood == 99,
        "unsafe eager control did not reproduce the stranded partial refund");
}

static void WaitsForDependentRefundAndPipelinesDisjointItems()
{
    Expect(QuickStackBatchDependencies.HasItemNameOverlap(
            new[] { "Wood" },
            new[] { "Wood", "Stone" }),
        "a later Wood chest did not create a settlement barrier");
    Expect(!QuickStackBatchDependencies.HasItemNameOverlap(
            new[] { "Wood" },
            new[] { "Stone" }),
        "disjoint Wood and Stone deposits were treated as dependent");

    TerminalProbe terminal = new TerminalProbe();
    FakeDepositTransport<int> transport = new FakeDepositTransport<int>();
    QuickStackBatchPipeline<int> pipeline = new QuickStackBatchPipeline<int>(terminal.Complete);
    int playerWood = 0;
    int nearestWood = 49;
    int fartherWood = 1;
    int resumeCount = 0;
    QuickStackDepositContinuation continuation = new QuickStackDepositContinuation(
        waitForSettlement: true,
        () => resumeCount++);

    Expect(pipeline.TryRequestValidation(() => true),
        "nearest Wood validation did not start");
    bool began = pipeline.TryBeginValidatedDeposit(
            transport.Begin,
            accepted =>
            {
                nearestWood += accepted;
                playerWood = 100 - accepted;
            },
            () => throw new InvalidOperationException("nearest Wood deposit was rejected"),
            () => throw new InvalidOperationException("nearest Wood deposit settled twice"),
            exception => throw new InvalidOperationException("nearest Wood settlement failed", exception),
            continuation.DepositSettled);
    continuation.CompleteBegin(began);
    Expect(began,
        "nearest Wood deposit did not start");
    Expect(transport.Pending.Count == 1 && resumeCount == 0,
        "dependent farther chest started before the nearest refund settled");

    transport.Pending[0](1);
    Expect(playerWood == 99 && nearestWood == 50 && resumeCount == 1,
        "nearest partial result did not refund 99 Wood before resuming");

    Expect(pipeline.TryRequestValidation(() => true),
        "farther Wood validation did not resume");
    Expect(pipeline.TryBeginValidatedDeposit(
            transport.Begin,
            accepted =>
            {
                fartherWood += accepted;
                playerWood -= accepted;
            },
            () => throw new InvalidOperationException("farther Wood deposit was rejected"),
            () => throw new InvalidOperationException("farther Wood deposit settled twice"),
            exception => throw new InvalidOperationException("farther Wood settlement failed", exception),
            () => { }),
        "refunded Wood was not offered to the farther chest");
    pipeline.StopScheduling("completed", "batch_finished");
    transport.Pending[1](99);
    Expect(playerWood == 0 && nearestWood == 50 && fartherWood == 100,
        "nearest-first fallback failed exact Wood conservation");
    terminal.ExpectSingle("completed", "batch_finished");

    int disjointResumeCount = 0;
    QuickStackDepositContinuation disjoint = new QuickStackDepositContinuation(
        waitForSettlement: false,
        () => disjointResumeCount++);
    disjoint.CompleteBegin(began: true);
    Expect(disjointResumeCount == 1,
        "disjoint item names did not continue scheduling immediately");
    disjoint.DepositSettled();
    Expect(disjointResumeCount == 1,
        "disjoint settlement repeated the immediate continuation");
}

static void ContinuationHandlesSynchronousRejectionFailureAndDuplicate()
{
    TerminalProbe rejectionTerminal = new TerminalProbe();
    QuickStackBatchPipeline<int> rejectionPipeline =
        new QuickStackBatchPipeline<int>(rejectionTerminal.Complete);
    int rejectionResumeCount = 0;
    QuickStackDepositContinuation rejection = new QuickStackDepositContinuation(
        waitForSettlement: true,
        () => rejectionResumeCount++);
    Expect(rejectionPipeline.TryRequestValidation(() => true),
        "synchronous rejection validation did not start");
    bool rejectedBegin = rejectionPipeline.TryBeginValidatedDeposit(
        _ => false,
        _ => throw new InvalidOperationException("rejected deposit unexpectedly settled"),
        () => { },
        () => throw new InvalidOperationException("rejected deposit settled twice"),
        exception => throw new InvalidOperationException("rejection callback failed", exception),
        rejection.DepositSettled);
    rejection.CompleteBegin(rejectedBegin);
    Expect(rejectedBegin && rejectionResumeCount == 1,
        "synchronous begin rejection did not resume exactly once");

    TerminalProbe failureTerminal = new TerminalProbe();
    FakeDepositTransport<int> transport = new FakeDepositTransport<int>();
    QuickStackBatchPipeline<int> failurePipeline =
        new QuickStackBatchPipeline<int>(failureTerminal.Complete);
    int failureResumeCount = 0;
    int failureCount = 0;
    int duplicateCount = 0;
    QuickStackDepositContinuation failure = new QuickStackDepositContinuation(
        waitForSettlement: true,
        () => failureResumeCount++);
    Expect(failurePipeline.TryRequestValidation(() => true),
        "throwing settlement validation did not start");
    bool failureBegin = failurePipeline.TryBeginValidatedDeposit(
        transport.Begin,
        _ => throw new InvalidOperationException("presentation failed"),
        () => throw new InvalidOperationException("throwing deposit was rejected"),
        () => duplicateCount++,
        _ => failureCount++,
        failure.DepositSettled);
    failure.CompleteBegin(failureBegin);
    Expect(failureBegin && failureResumeCount == 0,
        "barrier continued before the throwing settlement returned");
    transport.Pending[0](1);
    Expect(failureCount == 1 && failureResumeCount == 1,
        "throwing settlement did not resume exactly once");
    transport.Pending[0](1);
    Expect(duplicateCount == 1 && failureResumeCount == 1,
        "duplicate settlement repeated the continuation");
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
            _ => callbackFailures++,
            () => { }),
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
            exception => throw new InvalidOperationException("deposit callback failed", exception),
            () => { }),
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
