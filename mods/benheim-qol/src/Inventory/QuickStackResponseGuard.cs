using System;
using System.Collections.Generic;

namespace BenheimQoL.InventoryFeature;

/// <summary>
/// Tracks the response expected from the current Put Away chest request.
/// Valheim identifies the chest but not the request in its response. After a
/// timeout, the chest stays unavailable until the next response for that chest
/// is discarded, so no response can attach to a later Put Away operation.
/// </summary>
internal sealed class QuickStackResponseGuard<TContainer> where TContainer : class
{
    internal const float WaitSeconds = 5f;

    private readonly HashSet<TContainer> timedOutContainers = new HashSet<TContainer>();
    private TContainer? requestedContainer;
    private float requestedAt;

    internal bool TryBeginRequest(TContainer container, float now)
    {
        if (requestedContainer != null || timedOutContainers.Contains(container))
        {
            return false;
        }

        requestedContainer = container;
        requestedAt = now;
        return true;
    }

    internal bool TryTimeoutRequest(float now, out TContainer? container)
    {
        container = null;
        if (requestedContainer == null || now - requestedAt < WaitSeconds)
        {
            return false;
        }

        TContainer requested = requestedContainer!;
        container = requested;
        timedOutContainers.Add(requested);
        requestedContainer = null;
        return true;
    }

    internal void CompleteCurrentResponse(TContainer container)
    {
        if (requestedContainer != null
            && EqualityComparer<TContainer>.Default.Equals(requestedContainer, container))
        {
            requestedContainer = null;
        }
    }

    internal bool TryDiscardTimedOutResponse(TContainer container)
    {
        return timedOutContainers.Remove(container);
    }

    internal bool IsWaitingForTimedOutResponse(TContainer container)
    {
        return timedOutContainers.Contains(container);
    }

    internal void PruneTimedOutResponses(Func<TContainer, bool> shouldPrune)
    {
        timedOutContainers.RemoveWhere(container => shouldPrune(container));
    }

    internal void Reset()
    {
        requestedContainer = null;
        timedOutContainers.Clear();
    }
}
