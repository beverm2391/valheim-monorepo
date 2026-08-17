using System;
using System.Collections.Generic;

namespace BenheimQoL.PlayerCombat;

internal interface IEarnedStateOutput
{
    bool Activate(Player player, EarnedCombatState state, int tier);
    void Deactivate(Player player, EarnedCombatState state, int tier);
}

/// <summary>
/// Owns one player's ephemeral combat progress and earned states. Feature rules
/// may earn a state through this controller, while native output remains behind
/// one adapter.
/// </summary>
internal sealed class PlayerCombatController
{
    private readonly Player player;
    private readonly IEarnedStateOutput output;
    private readonly Dictionary<EarnedCombatState, int> activeStates =
        new Dictionary<EarnedCombatState, int>();

    internal PlayerCombatController(Player player, IEarnedStateOutput output)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.output = output ?? throw new ArgumentNullException(nameof(output));
    }

    internal int ConsecutivePerfectDefenses { get; private set; }

    internal void Observe(PerfectDefenseConfirmed perfectDefense)
    {
        if (perfectDefense.Context.Player == player)
        {
            ConsecutivePerfectDefenses++;
        }
    }

    internal void Observe(AcceptedPlayerDamage damage)
    {
        if (damage.After.Player != player || damage.HealthLost <= 0f)
        {
            return;
        }

        ConsecutivePerfectDefenses = 0;
        Deactivate(EarnedCombatState.Untouchable);
    }

    internal bool Earn(EarnedCombatState state, int tier)
    {
        if (tier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tier));
        }

        if (activeStates.TryGetValue(state, out int currentTier))
        {
            if (currentTier == tier)
            {
                return true;
            }

            Deactivate(state);
        }

        if (!output.Activate(player, state, tier))
        {
            return false;
        }

        activeStates.Add(state, tier);
        return true;
    }

    internal bool HasEarned(EarnedCombatState state)
    {
        return activeStates.ContainsKey(state);
    }

    internal int EarnedTier(EarnedCombatState state)
    {
        return activeStates.TryGetValue(state, out int tier) ? tier : 0;
    }

    internal void ForgetStoppedOutput(EarnedCombatState state, int tier)
    {
        if (activeStates.TryGetValue(state, out int currentTier) && currentTier == tier)
        {
            activeStates.Remove(state);
        }
    }

    internal void Reset()
    {
        ConsecutivePerfectDefenses = 0;
        // Keep cleanup order stable so native stop effects and diagnostics do
        // not depend on dictionary enumeration.
        Deactivate(EarnedCombatState.Clutch);
        Deactivate(EarnedCombatState.Untouchable);
        Deactivate(EarnedCombatState.Berserker);
    }

    private void Deactivate(EarnedCombatState state)
    {
        if (!activeStates.TryGetValue(state, out int tier))
        {
            return;
        }

        try
        {
            output.Deactivate(player, state, tier);
        }
        finally
        {
            activeStates.Remove(state);
        }
    }
}
