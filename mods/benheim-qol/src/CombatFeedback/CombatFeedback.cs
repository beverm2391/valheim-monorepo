using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.CombatFeedback;

internal static class CombatFeedbackController
{
    private const float FocusEndEpsilonDegrees = 0.01f;

    private enum FocusPhase
    {
        Idle,
        Drawing,
        Restoring
    }

    private static FocusPhase focusPhase;
    private static GameCamera? focusCamera;
    private static Player? focusPlayer;
    private static Camera? mainCamera;
    private static float currentFocusReduction;
    private static float focusReductionVelocity;

    private static float lastShakeAt = float.NegativeInfinity;
    private static float lastShakeStrength;
    private static CombatFeedbackTrigger lastShakeTrigger;

    internal static void UpdateBowFocus(GameCamera camera)
    {
        Player player = Player.m_localPlayer;
        Camera resolvedMainCamera = camera.GetComponent<Camera>();
        if (!BenheimFxSettings.BowFocusEnabled)
        {
            RestoreFocusSmoothly(camera, resolvedMainCamera, "benheim_fx_disabled");
            return;
        }

        string? blockReason = FocusBlockReason(player, resolvedMainCamera, camera.m_skyCamera);

        if (blockReason != null)
        {
            InterruptFocus(camera, resolvedMainCamera, blockReason);
            return;
        }

        if ((focusCamera && focusCamera != camera) || (focusPlayer && focusPlayer != player))
        {
            InterruptFocus(camera, resolvedMainCamera, "owner_changed");
        }

        focusCamera = camera;
        focusPlayer = player;
        mainCamera = resolvedMainCamera;

        float drawPercentage = player.GetAttackDrawPercentage();
        bool drawing = drawPercentage > 0f;
        if (drawing && focusPhase != FocusPhase.Drawing)
        {
            Diagnostics.Event(
                "CombatFeedback",
                "focus_started",
                $"draw={drawPercentage:0.###} base_fov={camera.m_fov:0.##} resumed={Diagnostics.Bool(focusPhase == FocusPhase.Restoring)}");
            focusPhase = FocusPhase.Drawing;
        }
        else if (!drawing)
        {
            RestoreFocusSmoothly(camera, resolvedMainCamera, "native_draw_inactive");
            return;
        }

        currentFocusReduction = Mathf.SmoothDamp(
            currentFocusReduction,
            CombatFeedbackTuning.FocusReduction(drawPercentage),
            ref focusReductionVelocity,
            CombatFeedbackTuning.BowFocusNarrowSmoothSeconds,
            float.PositiveInfinity,
            Time.unscaledDeltaTime);

        SetCameraFov(
            camera,
            resolvedMainCamera,
            Mathf.Max(1f, camera.m_fov - currentFocusReduction));
    }

    internal static void RequestShake(CombatFeedbackTrigger trigger)
    {
        if (!BenheimFxSettings.CombatShakeEnabled)
        {
            LogShakeSuppressed(trigger, "benheim_fx_disabled");
            return;
        }

        if (!HealthReporting.GameplayActionsEnabled)
        {
            LogShakeSuppressed(trigger, "gameplay_disabled");
            return;
        }

        GameCamera camera = GameCamera.instance;
        if (!camera || !Player.m_localPlayer)
        {
            LogShakeSuppressed(trigger, "camera_or_player_unavailable");
            return;
        }

        float strength = CombatFeedbackTuning.ShakeStrength(trigger);
        if (strength <= 0f)
        {
            LogShakeSuppressed(trigger, "no_tuning");
            return;
        }

        float now = Time.realtimeSinceStartup;
        bool inCoalesceWindow = now - lastShakeAt < CombatFeedbackTuning.ShakeCoalesceSeconds;
        if (!CombatFeedbackTuning.ShouldApplyShake(now - lastShakeAt, lastShakeStrength, strength))
        {
            Diagnostics.Event(
                "CombatFeedback",
                "shake_suppressed",
                $"trigger={TriggerName(trigger)} reason=coalesced previous_trigger={TriggerName(lastShakeTrigger)} previous_strength={lastShakeStrength:0.###}");
            return;
        }

        // Native AddShake keeps one intensity and replaces it only when the
        // new value is at least as strong. Suppressing equal and weaker rapid
        // requests prevents refresh, while a stronger request upgrades the
        // same native shake without additive stacking.
        camera.AddShake(
            camera.transform.position,
            CombatFeedbackTuning.ShakeRangeMeters,
            strength,
            continous: false);
        Diagnostics.Event(
            "CombatFeedback",
            "shake_triggered",
            $"trigger={TriggerName(trigger)} strength={strength:0.###} capped={Diagnostics.Bool(strength >= CombatFeedbackTuning.ShakeStrengthCap)} coalesced={Diagnostics.Bool(inCoalesceWindow)}");
        lastShakeAt = now;
        lastShakeStrength = strength;
        lastShakeTrigger = trigger;
    }

    internal static void Reset()
    {
        if (focusCamera && mainCamera)
        {
            SetCameraFov(focusCamera, mainCamera, focusCamera.m_fov);
        }

        ClearFocusState();
        lastShakeAt = float.NegativeInfinity;
        lastShakeStrength = 0f;
        lastShakeTrigger = default;
    }

    private static string? FocusBlockReason(Player player, Camera resolvedMainCamera, Camera skyCamera)
    {
        if (!HealthReporting.GameplayActionsEnabled)
        {
            return "gameplay_disabled";
        }

        if (!player)
        {
            return "no_local_player";
        }

        if (!resolvedMainCamera || !skyCamera)
        {
            return "camera_unavailable";
        }

        if (GameCamera.InFreeFly())
        {
            return "free_fly";
        }

        if (player.IsDead())
        {
            return "dead";
        }

        if (player.IsTeleporting())
        {
            return "teleporting";
        }

        if (player.InCutscene())
        {
            return "cutscene";
        }

        if (player.IsAttached())
        {
            return "attached";
        }

        return null;
    }

    private static void InterruptFocus(GameCamera camera, Camera? resolvedMainCamera, string reason)
    {
        if (focusPhase == FocusPhase.Idle)
        {
            return;
        }

        // Normal camera mode writes m_fov before this postfix. Free-fly does
        // not, so explicitly release Benheim's last narrowed value once when
        // the mode takes ownership of the camera.
        if (resolvedMainCamera && camera.m_skyCamera)
        {
            SetCameraFov(camera, resolvedMainCamera, camera.m_fov);
        }

        Diagnostics.Event(
            "CombatFeedback",
            "focus_interrupted",
            $"reason={reason} reduction={currentFocusReduction:0.###}");
        ClearFocusState();
    }

    private static void RestoreFocusSmoothly(
        GameCamera camera,
        Camera? resolvedMainCamera,
        string reason)
    {
        if (focusPhase == FocusPhase.Idle)
        {
            return;
        }

        if (!resolvedMainCamera || !camera.m_skyCamera)
        {
            InterruptFocus(camera, resolvedMainCamera, "camera_unavailable");
            return;
        }

        if (focusPhase != FocusPhase.Restoring)
        {
            focusPhase = FocusPhase.Restoring;
            Diagnostics.Event(
                "CombatFeedback",
                "focus_restoring",
                $"reason={reason} reduction={currentFocusReduction:0.###}");
        }

        currentFocusReduction = Mathf.SmoothDamp(
            currentFocusReduction,
            0f,
            ref focusReductionVelocity,
            CombatFeedbackTuning.BowFocusRestoreSmoothSeconds,
            float.PositiveInfinity,
            Time.unscaledDeltaTime);

        if (currentFocusReduction <= FocusEndEpsilonDegrees)
        {
            SetCameraFov(camera, resolvedMainCamera, camera.m_fov);
            Diagnostics.Event("CombatFeedback", "focus_ended", $"reason={reason}");
            ClearFocusState();
            return;
        }

        SetCameraFov(
            camera,
            resolvedMainCamera,
            Mathf.Max(1f, camera.m_fov - currentFocusReduction));
    }

    private static void SetCameraFov(GameCamera camera, Camera resolvedMainCamera, float fieldOfView)
    {
        resolvedMainCamera.fieldOfView = fieldOfView;
        camera.m_skyCamera.fieldOfView = fieldOfView;
    }

    private static void ClearFocusState()
    {
        focusPhase = FocusPhase.Idle;
        focusCamera = null;
        focusPlayer = null;
        mainCamera = null;
        currentFocusReduction = 0f;
        focusReductionVelocity = 0f;
    }

    private static void LogShakeSuppressed(CombatFeedbackTrigger trigger, string reason)
    {
        Diagnostics.Event(
            "CombatFeedback",
            "shake_suppressed",
            $"trigger={TriggerName(trigger)} reason={reason}");
    }

    private static string TriggerName(CombatFeedbackTrigger trigger)
    {
        return trigger switch
        {
            CombatFeedbackTrigger.Headshot => "headshot",
            CombatFeedbackTrigger.Cleave => "cleave",
            CombatFeedbackTrigger.MiningAoe => "mining_aoe",
            _ => "unknown"
        };
    }
}
