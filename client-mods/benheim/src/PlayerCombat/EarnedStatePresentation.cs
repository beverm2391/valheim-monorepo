using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.PlayerCombat;

/// <summary>
/// Projects one confirmed-defense dispatch through local native primitives.
/// Its adrenaline line and earned-state titles become one readable Bonus world
/// text, while multiple state entries share one native charm one-shot.
/// </summary>
internal sealed class EarnedStatePresentation
{
    private readonly List<string> pendingTitles = new List<string>();
    private PlayerCombatContext? pendingDefense;
    private EarnedStateTransition? pendingCharmTransition;

    internal void BeginPerfectDefense(PlayerCombatContext context)
    {
        pendingDefense = context;
        pendingTitles.Clear();
        pendingCharmTransition = null;
    }

    internal void Observe(EarnedStateTransition transition)
    {
        if (transition.Kind != EarnedStateTransitionKind.Activated
            || transition.Context.Player != Player.m_localPlayer)
        {
            return;
        }

        string text = transition.State switch
        {
            EarnedCombatState.Clutch => ClutchMechanic.ActivationText,
            EarnedCombatState.Untouchable =>
                UntouchableMechanic.ActivationTextForTier(transition.Tier),
            EarnedCombatState.Berserker =>
                BerserkerMechanic.ActivationTextForTier(transition.Tier),
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (object.ReferenceEquals(pendingDefense, transition.Context))
        {
            pendingTitles.Add(text);
            pendingCharmTransition ??= transition;

            return;
        }

        WorldFeedback.ShowAbovePlayer(transition.Context.Player, text);
        PlayCharmActivation(transition);
    }

    internal void CompletePerfectDefense(
        Player player,
        string? adrenalineLine,
        bool nativeCharmActivated)
    {
        if (pendingDefense == null || pendingDefense.Player != player)
        {
            return;
        }

        List<string> lines = new List<string>(pendingTitles.Count + 1);
        if (!string.IsNullOrEmpty(adrenalineLine))
        {
            lines.Add(adrenalineLine);
        }

        lines.AddRange(pendingTitles);
        if (lines.Count > 0)
        {
            WorldFeedback.ShowAbovePlayer(player, string.Join("\n", lines));
        }

        // Player.AddAdrenaline has completed by this point. If it filled an
        // equipped charm, Valheim already emitted this exact native one-shot.
        if (!nativeCharmActivated && pendingCharmTransition != null)
        {
            PlayCharmActivation(pendingCharmTransition);
        }

        Reset();
    }

    internal void Reset()
    {
        pendingDefense = null;
        pendingTitles.Clear();
        pendingCharmTransition = null;
    }

    private static void PlayCharmActivation(EarnedStateTransition transition)
    {
        EffectList activationEffects = transition.Context.Player.m_adrenalinePopEffects;
        if (activationEffects != null && activationEffects.HasEffects())
        {
            activationEffects.Create(
                transition.Context.Player.transform.position,
                Quaternion.identity);
            return;
        }

        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "earned_state_activation_fx_rejected")
                .String("state", transition.State.ToString())
                .Integer("tier", transition.Tier)
                .String("reason", "native_adrenaline_effects_unavailable"));
    }
}
