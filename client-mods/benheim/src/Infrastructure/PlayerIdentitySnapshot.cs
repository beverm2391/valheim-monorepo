using System;
using System.Collections.Generic;

namespace BenheimQoL.Infrastructure;

internal readonly struct PlayerIdentityObservation
{
    internal PlayerIdentityObservation(string platformUserId, string characterName)
    {
        PlatformUserId = platformUserId;
        CharacterName = characterName;
    }

    internal string PlatformUserId { get; }
    internal string CharacterName { get; }
}

internal readonly struct PlayerIdentityChange
{
    internal PlayerIdentityChange(string changeKind, string platformUserId, string characterName)
    {
        ChangeKind = changeKind;
        PlatformUserId = platformUserId;
        CharacterName = characterName;
    }

    internal string ChangeKind { get; }
    internal string PlatformUserId { get; }
    internal string CharacterName { get; }
}

/// <summary>
/// Diffs complete native player-list snapshots. Valheim republishes that list
/// periodically, so only new account identities and changed character names
/// become diagnostics. Replacing the snapshot also forgets departed accounts,
/// allowing a later rejoin to be observed once.
/// </summary>
internal sealed class PlayerIdentitySnapshot
{
    private Dictionary<string, string> previous = new Dictionary<string, string>(StringComparer.Ordinal);

    internal IReadOnlyList<PlayerIdentityChange> Observe(
        IEnumerable<PlayerIdentityObservation> observations)
    {
        Dictionary<string, string> current = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (PlayerIdentityObservation observation in observations)
        {
            if (string.IsNullOrWhiteSpace(observation.PlatformUserId))
            {
                continue;
            }

            string name = observation.CharacterName ?? string.Empty;
            if (name.Length == 0 && previous.TryGetValue(observation.PlatformUserId, out string? priorName))
            {
                // A temporarily empty roster name must not erase a known name
                // or cause the next complete player-list packet to look like a
                // second join.
                name = priorName;
            }
            current[observation.PlatformUserId] = name;
        }

        List<PlayerIdentityChange> changes = new List<PlayerIdentityChange>();
        foreach (KeyValuePair<string, string> observed in current)
        {
            if (!previous.TryGetValue(observed.Key, out string? previousName))
            {
                if (observed.Value.Length > 0)
                {
                    changes.Add(new PlayerIdentityChange(
                        "first_seen",
                        observed.Key,
                        observed.Value));
                }
                continue;
            }

            if (observed.Value.Length > 0 &&
                !string.Equals(previousName, observed.Value, StringComparison.Ordinal))
            {
                changes.Add(new PlayerIdentityChange(
                    "character_name_changed",
                    observed.Key,
                    observed.Value));
            }
        }

        previous = current;
        return changes;
    }

    internal void Reset()
    {
        previous.Clear();
    }
}
