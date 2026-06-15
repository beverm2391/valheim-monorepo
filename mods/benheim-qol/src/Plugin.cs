using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.benheim.qol";
    public const string PluginName = "BenheimQoL";
    public const string PluginVersion = "0.1.3";

    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony? harmony;

    private void Awake()
    {
        Log = Logger;
        harmony = new Harmony(PluginGuid);
        harmony.PatchAll();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void Update()
    {
        ShortcutOverlay.Update();
    }

    private void OnGUI()
    {
        ShortcutOverlay.Draw();
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }
}
