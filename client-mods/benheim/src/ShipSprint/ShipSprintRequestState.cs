namespace BenheimQoL.ShipSprint;

// Every compatible peer keeps this transient helm-input state. Only the current
// ship owner may consume it for physics, after validating the native controller.
internal sealed class ShipSprintRequestState
{
    internal bool Requested { get; private set; }
    internal long PlayerId { get; private set; }
    internal long PeerId { get; private set; }

    internal void Update(long playerId, long peerId, bool requested)
    {
        if (!requested)
        {
            Clear();
            return;
        }

        Requested = true;
        PlayerId = playerId;
        PeerId = peerId;
    }

    internal void Clear()
    {
        Requested = false;
        PlayerId = 0L;
        PeerId = 0L;
    }

    internal ShipSprintDecision Decide(
        bool physicsOwner,
        bool controllerValid,
        Ship.Speed speed)
    {
        if (!physicsOwner)
        {
            return ShipSprintDecision.Stopped("ownership_lost");
        }
        if (!Requested)
        {
            return ShipSprintDecision.Stopped("released");
        }
        if (!controllerValid)
        {
            Clear();
            return ShipSprintDecision.Stopped("controller_lost");
        }

        return ShipSprintRules.ShouldBoost(Requested, physicsOwner, controllerValid, speed)
            ? ShipSprintDecision.Boosting
            : ShipSprintDecision.Stopped("throttle_not_forward");
    }
}

internal readonly struct ShipSprintDecision
{
    private ShipSprintDecision(bool active, string reason)
    {
        Active = active;
        Reason = reason;
    }

    internal static ShipSprintDecision Boosting { get; } =
        new ShipSprintDecision(true, string.Empty);
    internal bool Active { get; }
    internal string Reason { get; }
    internal static ShipSprintDecision Stopped(string reason) =>
        new ShipSprintDecision(false, reason);
}
