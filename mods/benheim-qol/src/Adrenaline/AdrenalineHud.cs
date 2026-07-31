using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BenheimQoL.Adrenaline;

internal static class AdrenalineHud
{
    private const int DecayEstimateSteps = 48;
    private const float MinimumDecayRate = 0.001f;

    private static readonly FieldInfo DegenTimerField =
        AccessTools.Field(typeof(Player), "m_adrenalineDegenTimer");

    private static TMP_Text? decayLabel;

    internal static void Update(Hud hud, Player player)
    {
        EnsureLabel(hud);
        if (!decayLabel)
        {
            return;
        }

        float adrenaline = player.GetAdrenaline();
        float maximum = player.GetMaxAdrenaline();
        if (adrenaline <= 0f || maximum <= 0f)
        {
            decayLabel.gameObject.SetActive(false);
            return;
        }

        decayLabel.gameObject.SetActive(true);
        float delay = Mathf.Max(0f, (float)DegenTimerField.GetValue(player));
        if (delay > 0.05f)
        {
            decayLabel.text = $"Decay {delay:0.0}s";
            return;
        }

        float remaining = EstimateDecayTime(player, adrenaline, maximum);
        decayLabel.text = float.IsInfinity(remaining)
            ? "Decaying"
            : $"Decaying {remaining:0.0}s";
    }

    private static void EnsureLabel(Hud hud)
    {
        if (decayLabel && decayLabel.transform.parent == hud.m_adrenalineBarRoot)
        {
            return;
        }

        decayLabel = Object.Instantiate(hud.m_adrenalineText, hud.m_adrenalineBarRoot);
        decayLabel.name = "BenheimQoL_AdrenalineDecay";
        decayLabel.alignment = TextAlignmentOptions.TopLeft;
        decayLabel.fontStyle = FontStyles.Normal;
        decayLabel.fontSize *= 0.72f;
        decayLabel.enableAutoSizing = false;
        decayLabel.raycastTarget = false;

        RectTransform rect = decayLabel.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(8f, -4f);
        rect.sizeDelta = new Vector2(280f, 36f);
    }

    private static float EstimateDecayTime(Player player, float adrenaline, float maximum)
    {
        // Valheim defines decay as an amount-per-second curve over normalized fill.
        float amountPerStep = adrenaline / DecayEstimateSteps;
        float seconds = 0f;
        for (int i = 0; i < DecayEstimateSteps; i++)
        {
            float sample = adrenaline - (i + 0.5f) * amountPerStep;
            float rate = player.m_adrenalineDegen.Evaluate(sample / maximum);
            if (rate <= MinimumDecayRate)
            {
                return float.PositiveInfinity;
            }

            seconds += amountPerStep / rate;
        }

        return seconds;
    }
}
