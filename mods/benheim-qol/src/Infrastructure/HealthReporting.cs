using System;

namespace BenheimQoL.Infrastructure;

internal static class HealthReporting
{
    internal const string CoreFailureOwner = "Core";
    internal const string CoreFailureMessage =
        "Benheim disabled: Harmony patching failed; gameplay features are disabled.";
    internal const string CriticalPlayerMessage =
        "Benheim disabled: a required Valheim hook failed. Press Left Shift+B for details.";
    internal const string KeybindInspectionOwner = "Shortcuts";
    internal const string KillAttributionOwner = "BERSERKER";
    internal const string KillAttributionPlayerMessage =
        "BERSERKER unavailable: Benheim Server Support is missing or incompatible. Press Left Shift+B for details.";

    private static bool coreGameplayDisabled;
    private static bool keybindInspectionFailed;
    private static bool criticalMessageShown;
    private static bool criticalMessageFailureLogged;
    private static bool killAttributionMessageShown;
    private static bool killAttributionMessageFailureLogged;
    private static string? coreFailureDetail;
    private static string? keybindInspectionDetail;
    private static string? killAttributionDetail;

    internal static bool GameplayActionsEnabled => !coreGameplayDisabled;
    internal static string? CoreFailureDetail => coreFailureDetail;
    internal static string? KeybindInspectionDetail => keybindInspectionDetail;
    internal static string? KillAttributionDetail => killAttributionDetail;

    internal static void BeginSession()
    {
        coreGameplayDisabled = false;
        keybindInspectionFailed = false;
        criticalMessageShown = false;
        criticalMessageFailureLogged = false;
        killAttributionMessageShown = false;
        killAttributionMessageFailureLogged = false;
        coreFailureDetail = null;
        keybindInspectionDetail = null;
        killAttributionDetail = null;
    }

    internal static void DisableCore(Exception exception)
    {
        coreGameplayDisabled = true;
        if (coreFailureDetail != null)
        {
            return;
        }

        string exceptionText = exception.ToString();
        coreFailureDetail = exceptionText;
        Plugin.Log.LogError($"{CoreFailureMessage} {exceptionText}");
        Diagnostics.Event(
            "Health",
            "core_disabled",
            $"reason=harmony_patch_failed error={Diagnostics.Flatten(exception.Message)}");
    }

    internal static void ReportKeybindInspectionFailure(string detail)
    {
        if (keybindInspectionFailed)
        {
            return;
        }

        keybindInspectionFailed = true;
        keybindInspectionDetail =
            $"Could not inspect native keybinds; collision warnings are unavailable ({detail}).";
        Plugin.Log.LogWarning($"Benheim warning [{KeybindInspectionOwner}]: {keybindInspectionDetail}");
        Diagnostics.Event(
            "Health",
            "warning",
            $"owner={KeybindInspectionOwner} key=shortcuts.keybind_inspection detail={Diagnostics.Flatten(keybindInspectionDetail)}");
    }

    internal static void ReportKillAttributionUnavailable(string detail)
    {
        string message =
            $"Benheim Server Support is required for BERSERKER/SLAUGHTERHOUSE ({detail}).";
        if (killAttributionDetail == message)
        {
            return;
        }

        killAttributionDetail = message;
        Plugin.Log.LogWarning($"Benheim warning [{KillAttributionOwner}]: {message}");
        Diagnostics.Event(
            "Health",
            "warning",
            $"owner={KillAttributionOwner} detail={Diagnostics.Flatten(message)}");
    }

    internal static void ReportKillAttributionAvailable()
    {
        killAttributionDetail = null;
    }

    internal static void UpdateCriticalMessage()
    {
        if (Player.m_localPlayer == null)
        {
            return;
        }

        if (!GameplayActionsEnabled)
        {
            if (criticalMessageShown)
            {
                return;
            }

            try
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, CriticalPlayerMessage);
                criticalMessageShown = true;
            }
            catch (Exception ex)
            {
                if (!criticalMessageFailureLogged)
                {
                    criticalMessageFailureLogged = true;
                    Plugin.Log.LogWarning($"Benheim could not show its startup warning yet: {ex.Message}");
                }
            }

            return;
        }

        if (killAttributionDetail == null || killAttributionMessageShown)
        {
            return;
        }

        try
        {
            Player.m_localPlayer.Message(
                MessageHud.MessageType.Center,
                KillAttributionPlayerMessage);
            killAttributionMessageShown = true;
        }
        catch (Exception ex)
        {
            if (!killAttributionMessageFailureLogged)
            {
                killAttributionMessageFailureLogged = true;
                Plugin.Log.LogWarning(
                    $"Benheim could not show its BERSERKER compatibility warning yet: {ex.Message}");
            }
        }
    }
}
