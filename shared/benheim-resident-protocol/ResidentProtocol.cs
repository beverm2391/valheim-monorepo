namespace Benheim.Resident;

/// <summary>
/// Wire names and ZDO keys shared by Benheim clients and Server Support.
/// Keep versioned names stable within one compatible release; change the
/// version and all names together when the message contract changes.
/// </summary>
internal static class ResidentProtocol
{
    internal const int Version = 1;

    internal const string CapabilityRpc = "Benheim.Resident.v1.Capability";
    internal const string PlacementRequestRpc = "Benheim.Resident.v1.PlacementRequest";
    internal const string PlacementResultRpc = "Benheim.Resident.v1.PlacementResult";
    internal const string EncounterRequestRpc = "Benheim.Resident.v1.EncounterRequest";
    internal const string EncounterGrantRpc = "Benheim.Resident.v1.EncounterGrant";
    internal const string SpeechReplyRpc = "Benheim.Resident.v1.SpeechReply";
    internal const string SpeechResultRpc = "Benheim.Resident.v1.SpeechResult";

    // This per-object RPC is routed by ZNetView to the native tub's current
    // owner. Its callback accepts a request only when the server issued it.
    internal const string OwnerPlacementRpc = "Benheim.Resident.v1.OwnerPlacement";

    // The owner sends the outcome back to the server's global ZRoutedRpc.
    internal const string OwnerPlacementResultRpc = "Benheim.Resident.v1.OwnerPlacementResult";

    internal const string InvitedKey = "benheim_resident_invited_v1";
    internal const string GenerationKey = "benheim_resident_generation_v1";
}
