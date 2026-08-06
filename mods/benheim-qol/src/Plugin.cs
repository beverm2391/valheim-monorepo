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
    public const string PluginVersion = "0.1.45";

    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony? harmony;

    private void Awake()
    {
        Log = Logger;
        harmony = new Harmony(PluginGuid);
        harmony.PatchAll();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        Diagnostics.Event("Core", "session_start", $"version={PluginVersion}");
    }

    private void Update()
    {
        ShortcutOverlay.Update();
        DiagnosticLogExporter.Update();
        QuickStackReceiptHud.Update();
        QuickStackHotkey.Update();
    }

    private void OnDestroy()
    {
        PlantingPreview.DestroyGhosts();
        QuickStackReceiptHud.Destroy();
        ShortcutOverlay.Destroy();
        QuickStack.ResetState();
        Diagnostics.Event("Core", "session_end", $"version={PluginVersion}");
        harmony?.UnpatchSelf();
    }
}
