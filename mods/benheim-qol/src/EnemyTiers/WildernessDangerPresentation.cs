using System;
using BenheimQoL.CombatFeedback;
using BenheimQoL.Infrastructure;
using TMPro;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

internal static class WildernessDangerPresentation
{
    private const float SampleIntervalSeconds = 0.25f;
    private static readonly WildernessDangerTransitionTracker Tracker = new();

    private static Player? trackedPlayer;
    private static WildernessDanger? currentDanger;
    private static float nextSampleAt;
    private static string lastSuppressionReason = "";
    private static Heightmap.Biome? lastUnsupportedBiome;
    private static int fittedArrivalBannerId;
    private static int invalidArrivalBannerMeasurementId;

    internal static WildernessDanger? CurrentDanger => currentDanger;

    internal static void Update()
    {
        float now = Time.unscaledTime;
        if (now < nextSampleAt)
        {
            return;
        }

        nextSampleAt = now + SampleIntervalSeconds;
        Player? player = Player.m_localPlayer;
        if (!player)
        {
            ResetForLifecycle("player_missing");
            trackedPlayer = null;
            return;
        }

        if (trackedPlayer != player)
        {
            trackedPlayer = player;
            Tracker.ResetForLifecycle();
            currentDanger = null;
            LogLifecycleReset("player_changed");
        }

        if (player.IsDead())
        {
            ResetForLifecycle("player_dead");
            return;
        }

        if (player.IsTeleporting())
        {
            SuppressWithoutReset("teleporting");
            return;
        }

        if (player.InCutscene())
        {
            SuppressWithoutReset("cutscene");
            return;
        }

        if (player.IsSleeping())
        {
            SuppressWithoutReset("sleeping");
            return;
        }

        Heightmap.Biome biome = player.GetCurrentBiome();
        if (!BiomeStarChanceTuning.TryGetCurve(biome, out BiomeChanceCurve curve))
        {
            Tracker.LeaveTunedWilderness();
            currentDanger = null;
            if (lastUnsupportedBiome != biome)
            {
                lastUnsupportedBiome = biome;
                Diagnostics.Event(
                    "EnemyTiers",
                    "wilderness_danger_state",
                    $"stage=unclassified reason=untuned_biome biome={biome}");
            }

            lastSuppressionReason = "";
            return;
        }

        lastUnsupportedBiome = null;
        lastSuppressionReason = "";
        float distance = Utils.LengthXZ(player.transform.position);
        float chance = WildernessStarChance.ComposeChance(
            curve,
            distance,
            WorldGenerator.worldSize);
        bool presentationAvailable = CanPresentArrival();
        WildernessDangerTransition transition = Tracker.Observe(
            chance,
            now,
            presentationAvailable);
        currentDanger = Tracker.HasStableDanger ? Tracker.StableDanger : null;
        LogTransition(transition, biome, distance, chance);

        if (transition.ArrivalDanger is WildernessDanger arrivalDanger)
        {
            PresentArrival(arrivalDanger, biome, distance, chance);
        }
    }

    internal static void Reset()
    {
        WildernessMapHover.Reset();
        WildernessMinimapIndicator.Reset();

        Tracker.ResetForLifecycle();
        trackedPlayer = null;
        currentDanger = null;
        nextSampleAt = 0f;
        lastSuppressionReason = "";
        lastUnsupportedBiome = null;
        fittedArrivalBannerId = 0;
        invalidArrivalBannerMeasurementId = 0;
    }

    private static bool CanPresentArrival()
    {
        return BenheimFxSettings.DangerArrivalEnabled
            && !Hud.IsUserHidden()
            && MessageHud.instance != null
            && MessageHud.instance.m_biomeFoundPrefab != null
            && MessageHud.instance.m_biomeFoundStinger != null
            && Hud.instance != null
            && Hud.instance.m_damageScreen != null;
    }

    private static void PresentArrival(
        WildernessDanger danger,
        Heightmap.Biome biome,
        float distance,
        float chance)
    {
        MessageHud messageHud = MessageHud.instance;
        bool stingerAvailable = messageHud.m_biomeFoundStinger != null;
        messageHud.ShowBiomeFoundMsg(
            $"Entering a {WildernessDangerScale.StyledArrivalLabel(danger)} area...",
            playStinger: true);

        Hud.instance.DamageFlash();

        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_danger_arrival",
            $"outcome=queued danger={danger} biome={biome} " +
            $"distance={distance:0} adjusted_chance={chance:0.###} " +
            $"presentation=native_biome_found stinger_available={Diagnostics.Bool(stingerAvailable)} " +
            "vignette=native_damage_flash");
    }

    internal static void FitArrivalBanner(GameObject? banner)
    {
        if (!banner || banner.GetInstanceID() == fittedArrivalBannerId)
        {
            return;
        }

        TMP_Text? title = Utils.FindChild(banner.transform, "Title")?.GetComponent<TMP_Text>();
        if (!title || !IsDangerArrivalText(title.text))
        {
            return;
        }

        int bannerId = banner.GetInstanceID();
        float sourceFontSize = title.enableAutoSizing ? title.fontSizeMax : title.fontSize;
        float availableWidth = title.rectTransform.rect.width - title.margin.x - title.margin.z;
        float preferredWidth = title.GetPreferredValues(
            title.text,
            Mathf.Infinity,
            title.rectTransform.rect.height).x;
        if (availableWidth <= 0f || preferredWidth <= 0f)
        {
            if (invalidArrivalBannerMeasurementId != bannerId)
            {
                invalidArrivalBannerMeasurementId = bannerId;
                Diagnostics.Event(
                    "EnemyTiers",
                    "wilderness_danger_arrival",
                    $"outcome=rejected reason=banner_measurement_not_ready available_width={availableWidth:0.##} " +
                    $"preferred_width={preferredWidth:0.##}");
            }

            return;
        }

        fittedArrivalBannerId = bannerId;
        title.enableAutoSizing = false;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.maxVisibleLines = 1;
        title.overflowMode = TextOverflowModes.Overflow;
        title.fontSize = sourceFontSize * Mathf.Min(1f, availableWidth / preferredWidth);

        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_danger_arrival",
            $"outcome=banner_fitted line_count=1 available_width={availableWidth:0.##} " +
            $"preferred_width={preferredWidth:0.##} font_size={title.fontSize:0.##}");
    }

    private static bool IsDangerArrivalText(string text)
    {
        return text.StartsWith("Entering a ", StringComparison.Ordinal)
            && text.EndsWith(" area...", StringComparison.Ordinal)
            && (text.IndexOf("DANGEROUS", StringComparison.Ordinal) >= 0
                || text.IndexOf("DEADLY", StringComparison.Ordinal) >= 0);
    }

    private static void LogTransition(
        WildernessDangerTransition transition,
        Heightmap.Biome biome,
        float distance,
        float chance)
    {
        if (transition.BaselineEstablished)
        {
            Diagnostics.Event(
                "EnemyTiers",
                "wilderness_danger_state",
                $"stage=baseline reason=login_or_respawn_suppression danger={transition.CurrentDanger} " +
                $"biome={biome} distance={distance:0} adjusted_chance={chance:0.###}");
        }

        if (transition.CandidateStarted)
        {
            Diagnostics.Event(
                "EnemyTiers",
                "wilderness_danger_state",
                $"stage=candidate from={transition.PreviousDanger} to={transition.CurrentDanger} " +
                $"biome={biome} distance={distance:0} adjusted_chance={chance:0.###} " +
                $"debounce_seconds={WildernessDangerTransitionTracker.DebounceSeconds:0.##}");
        }

        if (transition.CandidateCancelled)
        {
            Diagnostics.Event(
                "EnemyTiers",
                "wilderness_danger_state",
                $"stage=candidate_rejected reason=returned_before_debounce danger={transition.CurrentDanger} " +
                $"biome={biome} distance={distance:0} adjusted_chance={chance:0.###}");
        }

        if (transition.StableChanged)
        {
            Diagnostics.Event(
                "EnemyTiers",
                "wilderness_danger_state",
                $"stage=stable from={transition.PreviousDanger} to={transition.CurrentDanger} " +
                $"biome={biome} distance={distance:0} adjusted_chance={chance:0.###}");
        }

        if (transition.ArrivalBlock == WildernessDangerArrivalBlock.Cooldown)
        {
            Diagnostics.Event(
                "EnemyTiers",
                "wilderness_danger_arrival",
                $"outcome=rejected reason=cooldown danger={transition.CurrentDanger} " +
                $"cooldown_remaining={transition.CooldownRemaining:0.##} biome={biome}");
        }
        else if (transition.ArrivalBlock == WildernessDangerArrivalBlock.PresentationUnavailable)
        {
            Diagnostics.Event(
                "EnemyTiers",
                "wilderness_danger_arrival",
                $"outcome=rejected reason=presentation_unavailable danger={transition.CurrentDanger} " +
                $"fx_enabled={Diagnostics.Bool(BenheimFxSettings.DangerArrivalEnabled)} " +
                $"hud_hidden={Diagnostics.Bool(Hud.IsUserHidden())} " +
                $"message_hud_available={Diagnostics.Bool(MessageHud.instance != null)} " +
                $"stinger_available={Diagnostics.Bool(MessageHud.instance != null && MessageHud.instance.m_biomeFoundStinger != null)} " +
                $"vignette_available={Diagnostics.Bool(Hud.instance != null && Hud.instance.m_damageScreen != null)} " +
                $"biome={biome}");
        }
    }

    private static void ResetForLifecycle(string reason)
    {
        Tracker.ResetForLifecycle();
        currentDanger = null;
        LogLifecycleReset(reason);
    }

    private static void SuppressWithoutReset(string reason)
    {
        Tracker.PauseObservation();
        currentDanger = null;
        if (lastSuppressionReason == reason)
        {
            return;
        }

        lastSuppressionReason = reason;
        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_danger_state",
            $"stage=suppressed reason={reason}");
    }

    private static void LogLifecycleReset(string reason)
    {
        if (lastSuppressionReason == reason)
        {
            return;
        }

        lastSuppressionReason = reason;
        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_danger_state",
            $"stage=reset reason={reason}");
    }

}
