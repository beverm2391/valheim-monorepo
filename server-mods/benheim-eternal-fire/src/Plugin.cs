using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BenheimEternalFire;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.benheim.eternalfire";
    public const string PluginName = "Benheim Eternal Fire";
    public const string PluginVersion = "0.1.1";
    public const string LoadMessage = "Benheim Eternal Fire 0.1.1 loaded after PatchAll.";

    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony? harmony;

    private void Awake()
    {
        Log = Logger;
        harmony = new Harmony(PluginGuid);
        harmony.PatchAll();
        Logger.LogInfo(LoadMessage);
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }
}
