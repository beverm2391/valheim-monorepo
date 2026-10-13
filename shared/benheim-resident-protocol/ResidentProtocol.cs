namespace Benheim.Resident;

internal enum ResidentEncounterTrigger
{
    Approach = 0,
    Talk = 1
}

/// <summary>
/// Wire names and ZDO keys shared by Benheim clients and Server Support.
/// Keep versioned names stable within one compatible release; change the
/// version and all names together when the message contract changes.
/// </summary>
internal static class ResidentProtocol
{
    internal const int Version = 3;

    internal const string TraceRpc = "Benheim.Resident.v3.SpeechTrace";

    internal const string CapabilityRpc = "Benheim.Resident.v3.Capability";
    internal const string PlacementRequestRpc = "Benheim.Resident.v3.PlacementRequest";
    internal const string PlacementResultRpc = "Benheim.Resident.v3.PlacementResult";
    internal const string EncounterRequestRpc = "Benheim.Resident.v3.EncounterRequest";
    internal const string EncounterGrantRpc = "Benheim.Resident.v3.EncounterGrant";
    internal const string SpeechReplyRpc = "Benheim.Resident.v3.SpeechReply";
    internal const string SpeechResultRpc = "Benheim.Resident.v3.SpeechResult";

    // This per-object RPC is routed by ZNetView to the native tub's current
    // owner. Its callback accepts a request only when the server issued it.
    internal const string OwnerPlacementRpc = "Benheim.Resident.v3.OwnerPlacement";

    // The owner sends the outcome back to the server's global ZRoutedRpc.
    internal const string OwnerPlacementResultRpc = "Benheim.Resident.v3.OwnerPlacementResult";

    // Invitation state persists on ordinary native tubs, so its saved keys do
    // not follow the transient RPC contract version.
    internal const string InvitedKey = "benheim_resident_invited_v1";
    internal const string GenerationKey = "benheim_resident_generation_v1";

    internal static bool TryParseEncounterTrigger(int value, out ResidentEncounterTrigger trigger)
    {
        switch (value)
        {
            case (int)ResidentEncounterTrigger.Approach:
                trigger = ResidentEncounterTrigger.Approach;
                return true;
            case (int)ResidentEncounterTrigger.Talk:
                trigger = ResidentEncounterTrigger.Talk;
                return true;
            default:
                trigger = default;
                return false;
        }
    }
}
