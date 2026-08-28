using BenheimQoL.Infrastructure;
using BenheimQoL.PlayerCombat;
using UnityEngine;

namespace BenheimQoL.Adrenaline;

internal static class AdrenalineFeedback
{
    private static string? currentSource;
    private static Award? pendingAward;

    internal static void ObservePerfectDefense(PerfectDefenseConfirmed perfectDefense)
    {
        if (perfectDefense.Context.Player != Player.m_localPlayer)
        {
            return;
        }

        currentSource = perfectDefense.Kind == PerfectDefenseKind.Parry
            ? "Perfect parry"
            : "Perfect dodge";
    }

    internal static void EndPerfectDefense()
    {
        currentSource = null;
    }

    internal static void Reset()
    {
        currentSource = null;
        pendingAward = null;
    }

    internal static Award? CaptureAward(Player player, float value)
    {
        if (player != Player.m_localPlayer || currentSource == null || value <= 0f)
        {
            return null;
        }

        float before = player.GetAdrenaline();
        float maximum = player.GetMaxAdrenaline();
        Diagnostics.Event(
            "Adrenaline",
            "award_captured",
            $"source=\"{currentSource}\" requested={value:0.###} before={before:0.###} maximum={maximum:0.###}");
        return new Award(currentSource, before, maximum);
    }

    internal static void BeginModifiedAmountCapture(Award? award, SEMan statusEffects)
    {
        if (award == null)
        {
            return;
        }

        award.StatusEffects = statusEffects;
        pendingAward = award;
    }

    internal static void CaptureModifiedAmount(SEMan statusEffects, float amount)
    {
        if (pendingAward?.StatusEffects == statusEffects)
        {
            pendingAward.NativeModifiedAmount = amount;
        }
    }

    internal static void EndModifiedAmountCapture(Award? award)
    {
        if (pendingAward == award)
        {
            pendingAward = null;
        }
    }

    internal static void ShowAward(Player player, Award? award)
    {
        string? text = null;
        bool nativeCharmActivated = false;
        if (award != null
            && award.NativeModifiedAmount.HasValue
            && award.Maximum > 0f)
        {
            nativeCharmActivated =
                award.Before + Mathf.Max(0f, award.NativeModifiedAmount.Value)
                    >= award.Maximum
                && player.GetAdrenaline() < award.Maximum;
            float headroom = Mathf.Max(0f, award.Maximum - award.Before);
            float applied = Mathf.Max(
                0f,
                Mathf.Min(award.NativeModifiedAmount.Value, headroom));
            if (applied > 0f)
            {
                float after = player.GetAdrenaline();
                text = $"{award.Source} +{applied:0.#}";
                Diagnostics.Event(
                    "Adrenaline",
                    "feedback_shown",
                    $"source=\"{award.Source}\" amount={applied:0.###} before={award.Before:0.###} after={after:0.###}");
            }
        }

        PlayerCombatRuntime.CompletePerfectDefensePresentation(
            player,
            text,
            nativeCharmActivated);
    }

    internal sealed class Award
    {
        internal Award(string source, float before, float maximum)
        {
            Source = source;
            Before = before;
            Maximum = maximum;
        }

        internal string Source { get; }
        internal float Before { get; }
        internal float Maximum { get; }
        internal SEMan? StatusEffects { get; set; }
        internal float? NativeModifiedAmount { get; set; }
    }
}
