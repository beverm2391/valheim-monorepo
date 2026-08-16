using BenheimServerSupport;
using System.Collections.Concurrent;

var lease = new PutAwayLeaseState<object>();
var first = new object();
var second = new object();

Expect(lease.TryAcquire(first, "first"), "first requester acquires the empty lease");
Expect(!lease.TryAcquire(second, "second"), "second requester is rejected while first holds the lease");
Expect(!lease.TryRelease(second, "first"), "another peer cannot release the lease");
Expect(!lease.TryRelease(first, "wrong"), "the owner cannot release a different operation");
Expect(lease.TryRelease(first, "first"), "the exact owner operation releases the lease");
Expect(lease.TryAcquire(second, "second"), "a later operation acquires after release");
Expect(lease.TryReleasePeer(second, out string released) && released == "second", "disconnect releases the owning peer operation");

lease.Reset();
var peers = Enumerable.Range(0, 64).Select(_ => new object()).ToArray();
var winners = new ConcurrentBag<int>();
Parallel.For(0, peers.Length, index =>
{
    if (lease.TryAcquire(peers[index], $"race-{index}"))
    {
        winners.Add(index);
    }
});
Expect(winners.Count == 1, "concurrent acquisition produces exactly one winner");
int winner = winners.Single();
Expect(lease.TryRelease(peers[winner], $"race-{winner}"), "the concurrent winner releases normally");

PutAwaySimulation safe = SimulateContention(enforceLease: true);
Expect(safe.Granted == 1, "leased contention grants exactly one client");
Expect(safe.Busy == 1, "leased contention returns busy to the loser");
Expect(safe.Scans == 1 && safe.NativeStacks == 1 && safe.SourceRemovals == 1, "busy rejection precedes every mutation phase");
Expect(safe.LaterGranted, "terminal release permits a later operation");

PutAwaySimulation unsafeControl = SimulateContention(enforceLease: false);
Expect(unsafeControl.Granted == 2 && unsafeControl.SourceRemovals == 2, "missing exclusion control reproduces two writers");

Console.WriteLine("Put Away lease exclusion checks passed.");

static void Expect(bool condition, string scenario)
{
    if (!condition)
    {
        throw new InvalidOperationException(scenario);
    }
}

static PutAwaySimulation SimulateContention(bool enforceLease)
{
    var state = new PutAwayLeaseState<object>();
    var result = new PutAwaySimulation();
    object[] contenders = { new object(), new object() };
    for (int index = 0; index < contenders.Length; index++)
    {
        string operationId = $"operation-{index}";
        bool granted = !enforceLease || state.TryAcquire(contenders[index], operationId);
        if (!granted)
        {
            result.Busy++;
            continue;
        }

        result.Granted++;
        result.Scans++;
        result.NativeStacks++;
        result.SourceRemovals++;
    }

    if (enforceLease)
    {
        state.TryRelease(contenders[0], "operation-0");
        result.LaterGranted = state.TryAcquire(new object(), "later");
    }
    return result;
}

internal sealed class PutAwaySimulation
{
    internal int Granted { get; set; }
    internal int Busy { get; set; }
    internal int Scans { get; set; }
    internal int NativeStacks { get; set; }
    internal int SourceRemovals { get; set; }
    internal bool LaterGranted { get; set; }
}
