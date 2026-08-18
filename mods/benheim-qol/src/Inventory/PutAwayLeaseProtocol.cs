namespace BenheimQoL.InventoryFeature;

internal static class PutAwayLeaseProtocol
{
    // Identifies the complete lease + inventory transaction pairing. Bump it
    // whenever either wire surface changes so every possible chest owner is
    // proven compatible before a requester scans or reserves items.
    internal const int Generation = 2;
    internal const string PeerReadyRpc = "Benheim.PutAway.Lease.PeerReady.v2";
    internal const string RequestRpc = "Benheim.PutAway.Lease.Request.v2";
    internal const string ResultRpc = "Benheim.PutAway.Lease.Result.v2";
    internal const string ReleaseRpc = "Benheim.PutAway.Lease.Release.v2";

    internal const string Granted = "granted";
    internal const string Rejected = "rejected";
}
