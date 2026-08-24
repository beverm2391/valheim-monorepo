namespace BenheimQoL.ShipSprint;

// The controller sends transitions immediately and renews only an active
// request. This keeps network traffic bounded while letting a new ship owner
// recover the held input after native ownership changes.
internal sealed class ShipSprintRequestCadence
{
    private bool initialized;
    private bool lastRequested;
    private float lastSentAt;

    internal bool ShouldSend(bool requested, float now)
    {
        bool changed = !initialized || lastRequested != requested;
        bool heartbeatDue = requested
            && now - lastSentAt >= ShipSprintTuning.RequestHeartbeatSeconds;
        if (!changed && !heartbeatDue)
        {
            return false;
        }

        initialized = true;
        lastRequested = requested;
        lastSentAt = now;
        return true;
    }

    internal void Reset()
    {
        initialized = false;
        lastRequested = false;
        lastSentAt = 0f;
    }
}
