using System;

namespace BenheimQoL.GreydwarfResident;

/// <summary>
/// Quiet nighttime rest is subordinate to visits and speech. Keeping the wake
/// deadline separate from the visit debounce lets a lingering visitor go quiet
/// again without provoking a fresh greeting every time George dozes off.
/// </summary>
internal sealed class ResidentDrowsiness
{
    private float awakeUntil;
    internal bool IsDrowsy { get; private set; }
    internal float Weight { get; private set; }

    internal void Reset(float now)
    {
        awakeUntil = now + 6f;
        IsDrowsy = false;
        Weight = 0f;
    }

    internal bool Wake(float now)
    {
        bool wasDrowsy = IsDrowsy;
        awakeUntil = now + 20f;
        IsDrowsy = false;
        return wasDrowsy;
    }

    internal bool Update(float now, float deltaTime, bool night, bool quiet)
    {
        bool wasDrowsy = IsDrowsy;
        IsDrowsy = night && quiet && now >= awakeUntil;
        // Ease into a four-second head droop; waking takes at most one second.
        // State changes immediately so an approach can interrupt the blend.
        float step = Math.Max(0f, deltaTime) * (IsDrowsy ? .25f : 1f);
        Weight = IsDrowsy ? Math.Min(1f, Weight + step) : Math.Max(0f, Weight - step);
        return wasDrowsy != IsDrowsy;
    }
}
