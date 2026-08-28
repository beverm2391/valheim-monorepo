namespace BenheimQoL.ShipSprint;

internal static class ShipSprintRules
{
    internal static bool IsForwardThrottle(Ship.Speed speed)
    {
        return speed == Ship.Speed.Slow || speed == Ship.Speed.Half || speed == Ship.Speed.Full;
    }

    internal static bool IsSailThrottle(Ship.Speed speed)
    {
        return speed == Ship.Speed.Half || speed == Ship.Speed.Full;
    }

    internal static bool ShouldBoost(
        bool requested,
        bool physicsOwner,
        bool controllerValid,
        Ship.Speed speed)
    {
        return requested && physicsOwner && controllerValid && IsForwardThrottle(speed);
    }

    internal static float ThrustMultiplier(bool shouldBoost)
    {
        return shouldBoost ? ShipSprintTuning.ThrustMultiplier : 1f;
    }

    internal static bool IsAuthorizedSender(
        long currentUser,
        long requestedPlayer,
        long controllingPeer,
        long sender,
        bool controllerValid)
    {
        return controllerValid
            && currentUser != 0L
            && currentUser == requestedPlayer
            && controllingPeer == sender;
    }
}
