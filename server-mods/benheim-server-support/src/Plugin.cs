using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BenheimServerSupport;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.benheim.serversupport";
    public const string PluginName = "Benheim Server Support";
    public const string PluginVersion = "0.1.1";

    internal static ManualLogSource Log { get; private set; } = null!;
    private Harmony? harmony;

    private void Awake()
    {
        Log = Logger;
        ServerDiagnostics.Begin(Paths.BepInExRootPath, PluginVersion);
        harmony = new Harmony(PluginGuid);
        harmony.PatchAll();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded with the Put Away lease coordinator.");
    }

    private void OnDestroy()
    {
        InventoryTransactionRuntime.Shutdown();
        PutAwayLeaseServer.Reset();
        harmony?.UnpatchSelf();
        ServerDiagnostics.End();
    }
}
