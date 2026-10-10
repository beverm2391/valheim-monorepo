using BenheimServerSupport;

var confirmation = new ResidentPlacementConfirmation(
    expectedGeneration: 4,
    desired: true,
    deadline: 12,
    lateWatchSeconds: 12);
Expect(confirmation.TryRecordOwnerResult(true, "accepted", 5), "the first authenticated owner result is recorded");
Expect(
    confirmation.Observe(1, hasReplicatedState: true, replicatedGeneration: 4, invited: false, out _) ==
        ResidentPlacementProgress.WaitingForReplica,
    "an owner acknowledgement arriving before ZDO replication remains pending");
Expect(
    confirmation.Observe(2, hasReplicatedState: true, replicatedGeneration: 5, invited: true, out _) ==
        ResidentPlacementProgress.Applied,
    "the matching replicated generation and desired flag confirm placement");

var replicaFirst = new ResidentPlacementConfirmation(4, true, 12, 12);
Expect(
    replicaFirst.Observe(1, hasReplicatedState: true, replicatedGeneration: 5, invited: true, out _) ==
        ResidentPlacementProgress.WaitingForOwner,
    "replicated state alone does not confirm an unauthenticated owner result");
Expect(replicaFirst.TryRecordOwnerResult(true, "accepted", 5), "the later owner acknowledgement is recorded");
Expect(
    replicaFirst.Observe(2, hasReplicatedState: true, replicatedGeneration: 5, invited: true, out _) ==
        ResidentPlacementProgress.Applied,
    "a late-arriving owner acknowledgement completes already-replicated state");

var late = new ResidentPlacementConfirmation(8, false, 12, 12);
Expect(
    late.Observe(12, hasReplicatedState: true, replicatedGeneration: 8, invited: true, out string timeoutReason) ==
        ResidentPlacementProgress.TimedOut && timeoutReason == "placement_timeout_unknown",
    "the request returns an explicit unknown outcome at its owner deadline");
Expect(late.TryRecordOwnerResult(true, "accepted", 9), "the still-bounded late watch accepts an owner acknowledgement");
Expect(
    late.Observe(13, hasReplicatedState: true, replicatedGeneration: 8, invited: true, out _) ==
        ResidentPlacementProgress.WaitingForReplica,
    "a late acknowledgement still waits for its replicated state");
Expect(
    late.Observe(14, hasReplicatedState: true, replicatedGeneration: 9, invited: false, out _) ==
        ResidentPlacementProgress.LateApplied,
    "a late matching mutation can still be persisted without sending a second client result");

var expired = new ResidentPlacementConfirmation(2, true, 12, 12);
Expect(expired.Observe(12, false, 0, false, out _) == ResidentPlacementProgress.TimedOut, "the visible deadline starts a late watch");
Expect(
    expired.Observe(24, false, 0, false, out _) == ResidentPlacementProgress.Expired,
    "the late watch is bounded and releases the pending slot");

var mismatch = new ResidentPlacementConfirmation(3, true, 12, 12);
mismatch.TryRecordOwnerResult(true, "accepted", 4);
Expect(
    mismatch.Observe(2, true, 4, false, out string mismatchReason) == ResidentPlacementProgress.Rejected &&
    mismatchReason == "owner_state_mismatch",
    "the generation alone is insufficient when the desired invitation flag disagrees");

var cohort = new ResidentPeerCohort<object>();
var compatiblePeer = new object();
var joiningPeer = new object();
Expect(cohort.Track(compatiblePeer), "the first peer enters the cohort");
Expect(
    cohort.TryRecordVersion(compatiblePeer, 1, out int? unknownVersion) && !unknownVersion.HasValue,
    "an authenticated capability request resolves that peer's unknown version");
long activeRevision = cohort.Revision;
Expect(cohort.Track(joiningPeer) && cohort.Revision > activeRevision,
    "a peer joining during an encounter advances the cohort revision");
Expect(
    cohort.TryGetVersion(joiningPeer, out int? joiningVersion) && !joiningVersion.HasValue,
    "a joining peer remains unknown until its own capability request arrives");
Expect(cohort.TryRecordVersion(joiningPeer, 1, out _), "the joining peer can later announce the current protocol");
long compatibleRevision = cohort.Revision;
Expect(
    cohort.TryRecordVersion(joiningPeer, 2, out int? priorVersion) && priorVersion == 1 && cohort.Revision > compatibleRevision,
    "a peer changing protocol versions invalidates the saved encounter cohort");
Expect(cohort.Remove(joiningPeer) && cohort.Revision > compatibleRevision + 1,
    "disconnect cleanup advances the cohort revision again");

Console.WriteLine("Resident placement confirmation tests passed.");

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
