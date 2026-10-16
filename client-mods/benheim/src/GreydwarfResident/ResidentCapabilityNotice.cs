namespace BenheimQoL.GreydwarfResident;

// Capability failures can arrive before the local Player exists. Keep one
// notice pending until the client can display it, and clear that pending
// failure if a later capability response makes the resident renderable again.
internal sealed class ResidentCapabilityNotice
{
    private bool receivedCapability;
    private bool pending;
    private bool delivered;

    // The first response is a meaningful state even when it matches the
    // client's MissingSupport initializer. Later responses only count when
    // readiness or shared-action availability changed.
    internal bool ObserveCapability(bool changed, bool canRender)
    {
        bool firstResponse = !receivedCapability;
        receivedCapability = true;

        if (canRender)
            Resolve();
        else if (firstResponse || changed)
            Request();

        return firstResponse || changed;
    }

    internal void Request()
    {
        if (delivered) return;
        pending = true;
    }

    // Return true exactly once when the native player and HUD can receive
    // the notice. The caller owns the actual Unity MessageHud interaction.
    internal bool TryTake(bool canDisplay)
    {
        if (!canDisplay || !pending) return false;
        pending = false;
        delivered = true;
        return true;
    }

    // A valid renderable response supersedes an undelivered timeout/failure.
    // Reset the per-incident latch too, so a later failure can be reported.
    private void Resolve()
    {
        pending = false;
        delivered = false;
    }

    internal void Reset()
    {
        receivedCapability = false;
        Resolve();
    }
}
