using BenheimServerSupport;
using BenheimQoL.InventoryFeature;
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

var peerReadiness = new PutAwayPeerReadinessState<object>();
var currentRequester = new object();
var chestOwner = new object();
peerReadiness.Track(currentRequester);
peerReadiness.Track(chestOwner);
Expect(
    peerReadiness.TryRecord(currentRequester, PutAwayLeaseProtocol.Generation),
    "the current requester records its authenticated connection generation");
PeerCompatibilitySimulation legacyOwner = SimulatePeerCompatibility(
    peerReadiness,
    new[] { currentRequester, chestOwner });
Expect(
    legacyOwner.RejectionReason == "peer_protocol_unknown"
    && !legacyOwner.LeaseGranted
    && !legacyOwner.Scanned
    && !legacyOwner.Reserved,
    "a current requester and server must reject a silent legacy chest owner before scanning or reservation");

Expect(
    peerReadiness.TryRecord(chestOwner, PutAwayLeaseProtocol.Generation - 1),
    "the explicit incompatible-generation control records on its authenticated connection");
PeerCompatibilitySimulation incompatiblePeer = SimulatePeerCompatibility(
    peerReadiness,
    new[] { currentRequester, chestOwner });
Expect(
    incompatiblePeer.RejectionReason == "peer_protocol_incompatible"
    && !incompatiblePeer.LeaseGranted
    && !incompatiblePeer.Scanned
    && !incompatiblePeer.Reserved,
    "an explicitly mismatched peer must reject the lease before scanning or reservation");

var unknownReadiness = new PutAwayPeerReadinessState<object>();
var joiningPeer = new object();
unknownReadiness.Track(currentRequester);
unknownReadiness.Track(chestOwner);
unknownReadiness.Track(joiningPeer);
Expect(
    unknownReadiness.TryRecord(currentRequester, PutAwayLeaseProtocol.Generation)
    && unknownReadiness.TryRecord(chestOwner, PutAwayLeaseProtocol.Generation),
    "the active peers record readiness before the unknown joining-peer timing control");
PeerCompatibilitySimulation unknownPeer = SimulatePeerCompatibility(
    unknownReadiness,
    new[] { currentRequester, chestOwner, joiningPeer });
Expect(
    unknownPeer.RejectionReason == "peer_protocol_unknown"
    && !unknownPeer.LeaseGranted
    && !unknownPeer.Scanned
    && !unknownPeer.Reserved,
    "an unregistered connecting peer must reject the lease before scanning or reservation");

var currentReadiness = new PutAwayPeerReadinessState<object>();
currentReadiness.Track(currentRequester);
currentReadiness.Track(chestOwner);
Expect(
    currentReadiness.TryRecord(currentRequester, PutAwayLeaseProtocol.Generation)
    && currentReadiness.TryRecord(chestOwner, PutAwayLeaseProtocol.Generation),
    "both current peers record the required Put Away generation");
PeerCompatibilitySimulation allCurrent = SimulatePeerCompatibility(
    currentReadiness,
    new[] { currentRequester, chestOwner });
Expect(
    allCurrent.RejectionReason == string.Empty
    && allCurrent.LeaseGranted
    && allCurrent.Scanned
    && allCurrent.Reserved,
    "current requester, server, and chest owner may enter reservation");

currentReadiness.Remove(chestOwner);
Expect(
    currentReadiness.AllConnectedPeersMatch(
        new[] { currentRequester },
        PutAwayLeaseProtocol.Generation,
        out _),
    "disconnect cleanup removes the departed peer from the readiness boundary");

var changingCohort = new PutAwayPeerReadinessState<object>();
var leaseAcrossCohort = new PutAwayLeaseState<object>();
var batchOwner = new object();
var initialOwner = new object();
changingCohort.Track(batchOwner);
changingCohort.Track(initialOwner);
Expect(
    changingCohort.TryRecord(batchOwner, PutAwayLeaseProtocol.Generation)
    && changingCohort.TryRecord(initialOwner, PutAwayLeaseProtocol.Generation)
    && changingCohort.AllConnectedPeersMatch(
        new[] { batchOwner, initialOwner },
        PutAwayLeaseProtocol.Generation,
        out _,
        out long acquiredCohortRevision)
    && leaseAcrossCohort.TryAcquireOrValidate(
        batchOwner,
        "changing-cohort-operation",
        acquiredCohortRevision) == PutAwayLeaseRequestDecision.Acquired,
    "the current cohort acquires the batch lease");
int reservations = 1;

var legacyJoiningAfterGrant = new object();
changingCohort.Track(legacyJoiningAfterGrant);
bool legacyJoinCompatible = changingCohort.AllConnectedPeersMatch(
    new[] { batchOwner, initialOwner, legacyJoiningAfterGrant },
    PutAwayLeaseProtocol.Generation,
    out string legacyJoinReason,
    out _);
if (legacyJoinCompatible)
{
    reservations++;
}
Expect(
    legacyJoinReason == "peer_protocol_unknown" && reservations == 1,
    "a legacy or unknown owner joining after grant blocks the next reservation");
Expect(
    !leaseAcrossCohort.TryAcquire(new object(), "overlapping-after-join"),
    "a cohort change must not free the lease before the holder stops and releases");

long joinedCohortRevision = 0L;
Expect(
    changingCohort.TryRecord(legacyJoiningAfterGrant, PutAwayLeaseProtocol.Generation)
    && changingCohort.AllConnectedPeersMatch(
        new[] { batchOwner, initialOwner, legacyJoiningAfterGrant },
        PutAwayLeaseProtocol.Generation,
        out _,
        out joinedCohortRevision),
    "the joining peer can later report the current generation");
PutAwayLeaseRequestDecision changedCohortValidation =
    leaseAcrossCohort.TryAcquireOrValidate(
        batchOwner,
        "changing-cohort-operation",
        joinedCohortRevision);
Expect(
    changedCohortValidation == PutAwayLeaseRequestDecision.CohortChanged
    && reservations == 1,
    "even a compatible after-grant join invalidates the old cohort before the next reservation");
Expect(
    !leaseAcrossCohort.TryAcquire(new object(), "overlapping-after-validation"),
    "cohort validation rejection retains the active lease until client release");
Expect(
    leaseAcrossCohort.TryRelease(batchOwner, "changing-cohort-operation")
    && leaseAcrossCohort.TryAcquire(new object(), "after-holder-release"),
    "only the stopped holder's terminal release admits the next batch");

var stableCohort = new PutAwayPeerReadinessState<object>();
var stableLease = new PutAwayLeaseState<object>();
stableCohort.Track(batchOwner);
stableCohort.Track(initialOwner);
long stableRevision = 0L;
Expect(
    stableCohort.TryRecord(batchOwner, PutAwayLeaseProtocol.Generation)
    && stableCohort.TryRecord(initialOwner, PutAwayLeaseProtocol.Generation)
    && stableCohort.AllConnectedPeersMatch(
        new[] { batchOwner, initialOwner },
        PutAwayLeaseProtocol.Generation,
        out _,
        out stableRevision)
    && stableLease.TryAcquireOrValidate(
        batchOwner,
        "stable-cohort-operation",
        stableRevision) == PutAwayLeaseRequestDecision.Acquired
    && stableLease.TryAcquireOrValidate(
        batchOwner,
        "stable-cohort-operation",
        stableRevision) == PutAwayLeaseRequestDecision.Validated,
    "an unchanged current cohort validates before each later reservation");

var disconnectCohort = new PutAwayPeerReadinessState<object>();
var disconnectLease = new PutAwayLeaseState<object>();
disconnectCohort.Track(batchOwner);
disconnectCohort.Track(initialOwner);
long beforeDisconnectRevision = 0L;
Expect(
    disconnectCohort.TryRecord(batchOwner, PutAwayLeaseProtocol.Generation)
    && disconnectCohort.TryRecord(initialOwner, PutAwayLeaseProtocol.Generation)
    && disconnectCohort.AllConnectedPeersMatch(
        new[] { batchOwner, initialOwner },
        PutAwayLeaseProtocol.Generation,
        out _,
        out beforeDisconnectRevision)
    && disconnectLease.TryAcquireOrValidate(
        batchOwner,
        "disconnect-cohort-operation",
        beforeDisconnectRevision) == PutAwayLeaseRequestDecision.Acquired,
    "the pre-disconnect cohort acquires its lease");
disconnectCohort.Remove(initialOwner);
Expect(
    disconnectCohort.AllConnectedPeersMatch(
        new[] { batchOwner },
        PutAwayLeaseProtocol.Generation,
        out _,
        out long afterDisconnectRevision)
    && disconnectLease.TryAcquireOrValidate(
        batchOwner,
        "disconnect-cohort-operation",
        afterDisconnectRevision) == PutAwayLeaseRequestDecision.CohortChanged
    && !disconnectLease.TryAcquire(new object(), "overlapping-after-disconnect"),
    "a disconnect after grant stops the next reservation without freeing the active lease");

var changedReadiness = new PutAwayPeerReadinessState<object>();
var changedReadinessLease = new PutAwayLeaseState<object>();
changedReadiness.Track(batchOwner);
changedReadiness.Track(initialOwner);
long beforeReadinessChangeRevision = 0L;
Expect(
    changedReadiness.TryRecord(batchOwner, PutAwayLeaseProtocol.Generation)
    && changedReadiness.TryRecord(initialOwner, PutAwayLeaseProtocol.Generation)
    && changedReadiness.AllConnectedPeersMatch(
        new[] { batchOwner, initialOwner },
        PutAwayLeaseProtocol.Generation,
        out _,
        out beforeReadinessChangeRevision)
    && changedReadinessLease.TryAcquireOrValidate(
        batchOwner,
        "readiness-change-operation",
        beforeReadinessChangeRevision) == PutAwayLeaseRequestDecision.Acquired,
    "the pre-change readiness cohort acquires its lease");
Expect(
    changedReadiness.TryRecord(initialOwner, PutAwayLeaseProtocol.Generation - 1)
    && !changedReadiness.AllConnectedPeersMatch(
        new[] { batchOwner, initialOwner },
        PutAwayLeaseProtocol.Generation,
        out string changedReadinessReason,
        out _)
    && changedReadinessReason == "peer_protocol_incompatible"
    && !changedReadinessLease.TryAcquire(new object(), "overlapping-after-readiness-change"),
    "a readiness change after grant stops the next reservation without freeing the active lease");

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

static PeerCompatibilitySimulation SimulatePeerCompatibility(
    PutAwayPeerReadinessState<object> readiness,
    IReadOnlyList<object> connectedPeers)
{
    bool peersCompatible = readiness.AllConnectedPeersMatch(
        connectedPeers,
        PutAwayLeaseProtocol.Generation,
        out string rejectionReason);
    var lease = new PutAwayLeaseState<object>();
    bool leaseGranted = peersCompatible
        && lease.TryAcquire(connectedPeers[0], "peer-compatibility-operation");
    bool scanned = leaseGranted;
    bool reserved = scanned;
    return new PeerCompatibilitySimulation(
        leaseGranted,
        scanned,
        reserved,
        rejectionReason);
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

internal sealed record PeerCompatibilitySimulation(
    bool LeaseGranted,
    bool Scanned,
    bool Reserved,
    string RejectionReason);
