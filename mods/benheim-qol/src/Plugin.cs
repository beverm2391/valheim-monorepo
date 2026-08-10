using System;
using BepInEx;
using BepInEx.Logging;
using BenheimQoL.Infrastructure;
using BenheimQoL.InventoryFeature;
using BenheimQoL.Farming;
using BenheimQoL.Repair;
using BenheimQoL.Shortcuts;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.benheim.qol";
    public const string PluginName = "Benheim";
    public const string PluginVersion = "0.1.52";

    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony? harmony;
    private bool patchCleanupFailureLogged;

    private void Awake()
    {
        Log = Logger;
        HealthReporting.BeginSession();
        try
        {
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll();
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
        ShortcutOverlay.Update();
        DiagnosticLogExporter.Update();
        if (!HealthReporting.GameplayActionsEnabled)
        {
            return;
        }

        TopLeftFeedbackHud.Update();
        QuickStack.Update();
        QuickStackHotkey.Update();
    }

    private void OnDestroy()
    {
        PlantingPreview.DestroyGhosts();
        TopLeftFeedbackHud.Destroy();
        ShortcutOverlay.Destroy();
        QuickStack.ResetState();
        Diagnostics.Event("Core", "session_end", $"version={PluginVersion}");
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
