using System;

namespace BenheimQoL.KillAttribution;

/// <summary>
/// Bounds capability discovery to one established Valheim server RPC. The
/// first attempt begins only after ZNet exposes that RPC as current, avoiding
/// the connection-callback race that can discard an otherwise valid response.
/// </summary>
internal sealed class KillAttributionCapabilityRetry
{
    private readonly float timeoutSeconds;
    private readonly float retryIntervalSeconds;
    private float deadline;
    private float nextAttempt;

    internal KillAttributionCapabilityRetry(
        float timeoutSeconds,
        float retryIntervalSeconds)
    {
        if (timeoutSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        }

        if (retryIntervalSeconds <= 0f || retryIntervalSeconds >= timeoutSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(retryIntervalSeconds));
        }

        this.timeoutSeconds = timeoutSeconds;
        this.retryIntervalSeconds = retryIntervalSeconds;
    }

    internal bool Started { get; private set; }
    internal bool Finished { get; private set; }
    internal int Attempts { get; private set; }

    internal void Begin(float now)
    {
        if (Started)
        {
            return;
        }

        Started = true;
        deadline = now + timeoutSeconds;
        nextAttempt = now;
    }

    internal bool TryBeginAttempt(float now, out int attempt)
    {
        attempt = 0;
        if (!Started || Finished || now >= deadline || now < nextAttempt)
        {
            return false;
        }

        Attempts++;
        attempt = Attempts;
        nextAttempt = now + retryIntervalSeconds;
        return true;
    }

    internal bool HasTimedOut(float now)
    {
        return Started && !Finished && now >= deadline;
    }

    internal void Finish()
    {
        Finished = true;
    }

    internal void Reset()
    {
        Started = false;
        Finished = false;
        Attempts = 0;
        deadline = 0f;
        nextAttempt = 0f;
    }
}
