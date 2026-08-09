using System;
using BenheimQoL.InventoryFeature;

QuickStackResponseGuard<string> guard = new QuickStackResponseGuard<string>();

Expect(guard.TryBeginRequest("chest-a", now: 0f), "first native request starts");
Expect(!guard.TryTimeoutRequest(QuickStackResponseGuard<string>.WaitSeconds - 0.01f, out _), "request remains active before the wait expires");
Expect(guard.TryTimeoutRequest(QuickStackResponseGuard<string>.WaitSeconds, out string? timedOut) && timedOut == "chest-a", "timeout quarantines exactly the requested chest");
Expect(!guard.TryBeginRequest("chest-a", now: 6f), "a new batch cannot overlap the abandoned chest request");
Expect(guard.TryBeginRequest("chest-b", now: 6f), "a later batch can run through a different chest after the timeout");
Expect(!guard.TryDiscardTimedOutResponse("chest-b"), "the new request is not treated as a late response");
Expect(guard.TryDiscardTimedOutResponse("chest-a"), "late response is consumed before it can attach to the new batch");
guard.CompleteCurrentResponse("chest-b");
Expect(guard.TryBeginRequest("chest-a", now: 7f), "discarding the late response makes that chest safe for a later attempt");
guard.CompleteCurrentResponse("chest-a");

Expect(guard.TryBeginRequest("chest-e", now: 7.5f), "second timeout test starts");
Expect(guard.TryTimeoutRequest(7.5f + QuickStackResponseGuard<string>.WaitSeconds, out timedOut) && timedOut == "chest-e", "timeout keeps the active request isolated");
Expect(guard.TryBeginRequest("chest-f", now: 13f), "the timeout does not keep the batch latch set");
Expect(guard.TryDiscardTimedOutResponse("chest-e"), "one late denial or grant releases only the timed-out chest");
Expect(!guard.TryDiscardTimedOutResponse("chest-e"), "only one late response is discarded");
guard.CompleteCurrentResponse("chest-f");

Expect(guard.TryBeginRequest("chest-c", now: 14f), "ordinary granted response test starts");
guard.CompleteCurrentResponse("chest-c");
Expect(guard.TryBeginRequest("chest-c", now: 15f), "ordinary granted response releases the serial request slot");
guard.CompleteCurrentResponse("chest-c");

Expect(guard.TryBeginRequest("chest-d", now: 16f), "ordinary denied response test starts");
guard.CompleteCurrentResponse("chest-d");
Expect(guard.TryBeginRequest("chest-d", now: 17f), "ordinary denied response releases the serial request slot");
guard.Reset();
Expect(guard.TryBeginRequest("chest-d", now: 18f), "reset clears all in-memory recovery state");

Console.WriteLine("quick-stack missing-response recovery checks passed");
return 0;

static void Expect(bool condition, string scenario)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Failed: {scenario}");
    }
}
