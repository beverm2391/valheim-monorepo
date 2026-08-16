namespace BenheimQoL.InventoryFeature;

internal static class PutAwayLeaseProtocol
{
    internal const string RequestRpc = "Benheim.PutAway.Lease.Request.v1";
    internal const string ResultRpc = "Benheim.PutAway.Lease.Result.v1";
    internal const string ReleaseRpc = "Benheim.PutAway.Lease.Release.v1";

    internal const string Granted = "granted";
    internal const string Rejected = "rejected";
}
