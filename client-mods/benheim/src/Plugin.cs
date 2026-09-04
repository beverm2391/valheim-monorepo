using System;
using System.Threading;
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
using BenheimQoL.ValheimDev;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.benheim.qol";
    public const string PluginName = "Benheim";
    public const string PluginVersion = "0.1.87";

    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony? harmony;
    private bool patchCleanupFailureLogged;

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
        BenheimTestCommandClient.InitializeConsole();
        DeveloperDiagnosticsRuntime.InitializeConsole();
        BenheimFxSettings.Initialize(Config);
        HealthReporting.BeginSession();
        ValheimDevRuntime.Initialize(
            Paths.BepInExRootPath,
            PluginVersion,
            Thread.CurrentThread.ManagedThreadId);
        try
        {
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll();
            if (ObjectDB.instance != null)
            {
                PlayerCombatRuntime.RegisterNativeEffects(ObjectDB.instance);
            }
        }
        catch (Exception ex)
        {
            TryRemoveFailedPatches(logFailure: true);
            HealthReporting.DisableCore(ex);
        }

        if (HealthReporting.GameplayActionsEnabled)
        {
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
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
        if (!HealthReporting.GameplayActionsEnabled && harmony != null)
        {
            TryRemoveFailedPatches(logFailure: false);
        }

        HealthReporting.UpdateCriticalMessage();
        KillAttributionClient.Update();
        RemoteDiagnostics.Update();
        ShortcutOverlay.Update();
        DiagnosticLogExporter.Update();
        DeveloperDiagnosticsRuntime.Update();
        ValheimDevRuntime.Update();
        FarmingGridPicker.Update();
        if (!HealthReporting.GameplayActionsEnabled)
        {
            return;
        }

        NativeConsoleShortcut.Update();
        TopLeftFeedbackHud.Update();
        BenheimTestCommandClient.Update();
        WildernessDangerPresentation.Update();
        QuickStack.Update();
        QuickStackHotkey.Update();
    }

    private void OnDestroy()
    {
        ValheimDevRuntime.Revoke("plugin_teardown");
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
        RemoteDiagnostics.Reset();
        PlayerCombatRuntime.EndSession();
        LungeRuntime.ResetSession();
        Diagnostics.Event("Core", "session_end", $"version={PluginVersion}");
        Diagnostics.EndSession();
        TryRemoveFailedPatches(logFailure: false);
    }

    private void TryRemoveFailedPatches(bool logFailure)
    {
        try
        {
            harmony?.UnpatchSelf();
            harmony = null;
            if (patchCleanupFailureLogged)
            {
                Logger.LogInfo("Benheim removed its partial Harmony patches after retrying cleanup.");
                Diagnostics.Event("Health", "partial_patches_removed");
            }
        }
        catch (Exception cleanupException)
        {
            if (logFailure || !patchCleanupFailureLogged)
            {
                Logger.LogError($"Benheim could not remove partial Harmony patches: {cleanupException}");
            }

            patchCleanupFailureLogged = true;
        }
    }
}
