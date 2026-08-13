using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BenheimTestCommands;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.benheim.testcommands";
    public const string PluginName = "Benheim Test Commands";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource Log { get; private set; } = null!;
    private Harmony? harmony;

    private void Awake()
    {
        Log = Logger;
        ServerDiagnostics.Begin(Paths.BepInExRootPath, PluginVersion);
        harmony = new Harmony(PluginGuid);
        harmony.PatchAll();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded with direct peer RPC authorization.");
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
        ServerDiagnostics.End();
    }
}
