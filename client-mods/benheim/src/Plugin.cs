using System;
using BepInEx;
using BepInEx.Logging;
using BenheimQoL.CombatFeedback;
using BenheimQoL.DeveloperDiagnostics;
using BenheimQoL.Infrastructure;
using BenheimQoL.InventoryFeature;
using BenheimQoL.Farming;
using BenheimQoL.EnemyTiers;
using BenheimQoL.Repair;
using BenheimQoL.Shortcuts;
using BenheimQoL.PlayerCombat;
using BenheimQoL.KillAttribution;
using BenheimQoL.ShipSprint;
using BenheimQoL.WorldLabels;
using BenheimQoL.Affinities;
using UnityEngine;

namespace BenheimQoL;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.benheim.qol";
    public const string PluginName = "Benheim";
    public const string PluginVersion = "0.1.97";

    internal static ManualLogSource Log { get; private set; } = null!;

    private PatchGroupManager? patchGroups;

    private void Awake()
    {
        Log = Logger;
        Diagnostics.BeginSession(Paths.BepInExRootPath, PluginVersion);
        FarmingGridPicker.Reset();
        LungeRuntime.ResetSession();
        PlayerCombatRuntime.BeginSession();
        DiagnosticsSharingSettings.Initialize(Config);
        RemoteDiagnostics.Begin(Paths.ConfigPath);
        DiagnosticsSharingSettings.ApplyLegacyPrivateTestDefault(
            RemoteDiagnostics.IsConfigured);
        RuntimeFailureCapture.Begin(Paths.BepInExRootPath);
        BenheimTestCommandClient.InitializeConsole();
        DeveloperDiagnosticsRuntime.InitializeConsole();
        BenheimFxSettings.Initialize(Config);
        HealthReporting.BeginSession();
        try
        {
            patchGroups = PatchGroupManager.Apply(
                typeof(Plugin).Assembly.GetTypes(),
                PluginGuid,
                HealthReporting.ReportPatchGroupFailure,
                HealthReporting.ReportPatchCleanupFailure,
                HealthReporting.ReportPatchCleanupSucceeded);
            if (IsPatchGroupAvailable(typeof(PlayerCombatRuntime)) && ObjectDB.instance != null)
            {
                PlayerCombatRuntime.RegisterNativeEffects(ObjectDB.instance);
            }
        }
        catch (Exception ex)
        {
            patchGroups?.UnpatchAll();
            HealthReporting.DisableCore(ex);
        }

        if (HealthReporting.GameplayActionsEnabled)
        {
            if (HealthReporting.PatchGroupFailures.Count == 0)
            {
                Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
            }
            else
            {
                Logger.LogWarning(
                    $"{PluginName} {PluginVersion} loaded with {HealthReporting.PatchGroupFailures.Count} unavailable patch group(s).");
            }
            Diagnostics.Event("Core", "session_start", $"version={PluginVersion}");
        }
        else
        {
            Logger.LogError($"{PluginName} {PluginVersion} loaded with gameplay disabled.");
            Diagnostics.Event("Core", "loaded_with_gameplay_disabled", $"version={PluginVersion}");
        }
    }

    private void Update()
    {
        patchGroups?.RetryFailedCleanup();

        HealthReporting.UpdateCriticalMessage();
        if (IsPatchGroupAvailable(typeof(KillAttributionClient)))
        {
            KillAttributionClient.Update();
        }
        RemoteDiagnostics.Update();
        RuntimeFailureCapture.Update();
        ShortcutOverlay.Update();
        DiagnosticLogExporter.Update();
        DeveloperDiagnosticsRuntime.Update();
        if (IsPatchGroupAvailable(typeof(FarmingGridPicker)))
        {
            FarmingGridPicker.Update();
        }
        if (!HealthReporting.GameplayActionsEnabled)
        {
            return;
        }

        if (IsPatchGroupAvailable(typeof(NativeConsoleShortcut)))
        {
            NativeConsoleShortcut.Update();
        }
        TopLeftFeedbackHud.Update();
        BenheimTestCommandClient.Update();
        if (IsPatchGroupAvailable(typeof(WildernessDangerPresentationPatches)))
        {
            WildernessDangerPresentation.Update();
        }
        if (IsPatchGroupAvailable(typeof(QuickStack)))
        {
            QuickStack.Update();
            QuickStackHotkey.Update();
        }
    }

    private void OnDestroy()
    {
        WorldLabelRuntime.Reset();
        ShipSprintRuntime.Reset("plugin_teardown");
        PlantingPreview.DestroyGhosts();
        FarmingGridPicker.Reset();
        CombatFeedbackController.Reset();
        TopLeftFeedbackHud.Destroy();
        WildernessDangerPresentation.Reset();
        DeveloperDiagnosticsRuntime.Reset();
        BenheimTestCommandClient.Reset();
        ShortcutOverlay.Destroy();
        QuickStack.ResetState();
        RuntimeFailureCapture.Update();
        RuntimeFailureCapture.End();
        RemoteDiagnostics.Reset();
        PlayerCombatRuntime.EndSession();
        LungeRuntime.ResetSession();
        Diagnostics.Event("Core", "session_end", $"version={PluginVersion}");
        Diagnostics.EndSession();
        patchGroups?.UnpatchAll();
    }

    private bool IsPatchGroupAvailable(Type featureType)
    {
        return HealthReporting.GameplayActionsEnabled
            && patchGroups?.IsAvailable(featureType) == true;
    }
}
