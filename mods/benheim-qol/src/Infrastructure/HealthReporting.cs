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

    private static bool coreGameplayDisabled;
    private static bool keybindInspectionFailed;
    private static bool criticalMessageShown;
    private static bool criticalMessageFailureLogged;
    private static string? coreFailureDetail;
    private static string? keybindInspectionDetail;

    internal static bool GameplayActionsEnabled => !coreGameplayDisabled;
    internal static string? CoreFailureDetail => coreFailureDetail;
    internal static string? KeybindInspectionDetail => keybindInspectionDetail;

    internal static void BeginSession()
    {
        coreGameplayDisabled = false;
        keybindInspectionFailed = false;
        criticalMessageShown = false;
        criticalMessageFailureLogged = false;
        coreFailureDetail = null;
        keybindInspectionDetail = null;
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

    internal static void UpdateCriticalMessage()
    {
        if (GameplayActionsEnabled
            || Player.m_localPlayer == null
            || criticalMessageShown)
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
    }
}
