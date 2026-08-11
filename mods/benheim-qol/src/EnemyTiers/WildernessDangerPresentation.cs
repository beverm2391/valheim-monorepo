using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.EnemyTiers;

internal static class WildernessDangerPresentation
{
    private const float SampleIntervalSeconds = 0.25f;
    private const float DangerousVignetteAlpha = 0.55f;
    private const float DeadlyVignetteAlpha = 0.85f;

    private static readonly WildernessDangerTransitionTracker Tracker = new();

    private static Player? trackedPlayer;
    private static WildernessDanger? currentDanger;
    private static float nextSampleAt;
    private static string lastSuppressionReason = "";
    private static Heightmap.Biome? lastUnsupportedBiome;
    private static string lastMinimapLogKey = "";

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
        Tracker.ResetForLifecycle();
        trackedPlayer = null;
        currentDanger = null;
        nextSampleAt = 0f;
        lastSuppressionReason = "";
        lastUnsupportedBiome = null;
        lastMinimapLogKey = "";
    }

    internal static void UpdateMinimapLabel(Minimap minimap)
    {
        Player? player = Player.m_localPlayer;
        if (!player)
        {
            LogMinimapOnce("rejected:player_missing", "outcome=rejected reason=player_missing");
            return;
        }

        if (minimap.m_biomeNameSmall == null)
        {
            LogMinimapOnce("rejected:label_missing", "outcome=rejected reason=native_label_missing");
            return;
        }

        string nativeBiome = Localization.instance.Localize(
            "$biome_" + player.GetCurrentBiome().ToString().ToLowerInvariant());
        minimap.m_biomeNameSmall.text = currentDanger is WildernessDanger danger
            ? $"{nativeBiome}  <size=70%>{WildernessDangerScale.StyledLabel(danger)}</size>"
            : nativeBiome;
        string dangerValue = currentDanger?.ToString() ?? "none";
        LogMinimapOnce(
            $"rendered:{player.GetCurrentBiome()}:{dangerValue}",
            $"outcome=rendered biome={player.GetCurrentBiome()} danger={dangerValue}");
    }

    private static bool CanPresentArrival()
    {
        return !Hud.IsUserHidden()
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
            $"Entering a {WildernessDangerScale.StyledLabel(danger)} area...",
            playStinger: true);

        Image damageScreen = Hud.instance.m_damageScreen;
        float requestedAlpha = danger == WildernessDanger.Deadly
            ? DeadlyVignetteAlpha
            : DangerousVignetteAlpha;
        Color color = damageScreen.color;
        color.a = Mathf.Max(color.a, requestedAlpha);
        damageScreen.color = color;
        damageScreen.gameObject.SetActive(value: true);

        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_danger_arrival",
            $"outcome=queued danger={danger} biome={biome} " +
            $"distance={distance:0} adjusted_chance={chance:0.###} " +
            $"presentation=native_biome_found stinger_available={Diagnostics.Bool(stingerAvailable)} " +
            $"vignette=native_damage_screen vignette_alpha={requestedAlpha:0.##}");
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

    private static void LogMinimapOnce(string key, string fields)
    {
        if (lastMinimapLogKey == key)
        {
            return;
        }

        lastMinimapLogKey = key;
        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_minimap_indicator",
            fields);
    }
}

[HarmonyPatch]
internal static class WildernessDangerPresentationPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Minimap), "UpdateBiome")]
    private static void UpdateBiomePostfix(Minimap __instance)
    {
        WildernessDangerPresentation.UpdateMinimapLabel(__instance);
    }
}
