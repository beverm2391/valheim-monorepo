using System;
using System.Runtime.CompilerServices;

namespace BenheimQoL.PlayerCombat;

/// <summary>
/// Remembers confirmed native outcome objects by reference. The weak table
/// rejects delayed replays without retaining completed attacks for the whole
/// session and without applying a time debounce to unrelated outcomes.
/// </summary>
internal sealed class PerfectDefenseOutcomeDeduplicator
{
    private ConditionalWeakTable<object, TokenHolder> accepted =
        new ConditionalWeakTable<object, TokenHolder>();
    private int lastToken;

    internal bool TryAccept(object identity, out int token)
    {
        if (identity == null)
        {
            throw new ArgumentNullException(nameof(identity));
        }

        if (accepted.TryGetValue(identity, out TokenHolder? existing))
        {
            token = existing.Token;
            return false;
        }

        lastToken = checked(lastToken + 1);
        token = lastToken;
        accepted.Add(identity, new TokenHolder(token));
        return true;
    }

    internal void Reset()
    {
        accepted = new ConditionalWeakTable<object, TokenHolder>();
        lastToken = 0;
    }

    private sealed class TokenHolder
    {
        internal TokenHolder(int token)
        {
            Token = token;
        }

        internal int Token { get; }
    }
}

/// <summary>
/// Gives each native Attack trigger a stable reference identity. Repeated hit
/// reports from one trigger resolve to the same object, while another trigger
/// on a looping attack receives a new object immediately.
/// </summary>
internal sealed class NativeAttackOutcomeIdentities<TAttack>
    where TAttack : class
{
    private ConditionalWeakTable<TAttack, CurrentOutcome> outcomes =
        new ConditionalWeakTable<TAttack, CurrentOutcome>();

    internal object Begin(TAttack attack)
    {
        if (attack == null)
        {
            throw new ArgumentNullException(nameof(attack));
        }

        CurrentOutcome current = outcomes.GetValue(
            attack,
            _ => new CurrentOutcome());
        current.Identity = new object();
        return current.Identity;
    }

    internal object GetOrBegin(TAttack attack, out bool triggerObserved)
    {
        if (attack == null)
        {
            throw new ArgumentNullException(nameof(attack));
        }

        if (outcomes.TryGetValue(attack, out CurrentOutcome? current)
            && current.Identity != null)
        {
            triggerObserved = true;
            return current.Identity;
        }

        triggerObserved = false;
        return Begin(attack);
    }

    internal void Reset()
    {
        outcomes = new ConditionalWeakTable<TAttack, CurrentOutcome>();
    }

    private sealed class CurrentOutcome
    {
        internal object? Identity { get; set; }
    }
}
