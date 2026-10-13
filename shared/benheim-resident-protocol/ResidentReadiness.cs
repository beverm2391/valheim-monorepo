namespace Benheim.Resident;

// Readiness of shared actions is stricter than the cosmetic resident's life.
// A join is not evidence that a player is running incompatible code.
internal enum ResidentReadiness
{
    Ready = 0,
    Connecting = 1,
    AwaitingSupport = 2,
    MissingSupport = 3,
    Incompatible = 4
}

internal static class ResidentReadinessPolicy
{
    internal static bool CanRender(ResidentReadiness state) =>
        state is ResidentReadiness.Ready or ResidentReadiness.Connecting or ResidentReadiness.AwaitingSupport;

    internal static bool TryParse(int value, out ResidentReadiness state)
    {
        state = (ResidentReadiness)value;
        return value >= (int)ResidentReadiness.Ready && value <= (int)ResidentReadiness.Incompatible;
    }

    internal static string Reason(ResidentReadiness state) => state switch
    {
        ResidentReadiness.Ready => "all_peers_ready",
        ResidentReadiness.Connecting => "peer_connecting",
        ResidentReadiness.AwaitingSupport => "peer_support_pending",
        ResidentReadiness.MissingSupport => "peer_support_missing",
        _ => "peer_protocol_incompatible"
    };
}
