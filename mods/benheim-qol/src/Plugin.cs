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
    public const string PluginName = "BenheimQoL";
    public const string PluginVersion = "0.1.16";

    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony? harmony;

    private void Awake()
    {
        Log = Logger;
        harmony = new Harmony(PluginGuid);
        harmony.PatchAll();
        BuildingRepairPatch.LogPatchStatus();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        Diagnostics.Event("Core", "session_start", $"version={PluginVersion}");
    }

    private void Update()
    {
        BuildingRepair.LogRepairInput();
        QuickStack.Update();
        QuickStackHotkey.Update();
        ShortcutOverlay.Update();
    }

    private void OnGUI()
    {
        ShortcutOverlay.Draw();
    }

    private void OnDestroy()
    {
        PlantingPreview.DestroyGhosts();
        Diagnostics.Event("Core", "session_end", $"version={PluginVersion}");
        harmony?.UnpatchSelf();
    }
}
