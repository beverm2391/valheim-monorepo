using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BenheimServerSupport;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.benheim.serversupport";
    public const string PluginName = "Benheim Server Support";
    public const string PluginVersion = "0.1.7";

    internal static ManualLogSource Log { get; private set; } = null!;
    private Harmony? harmony;

    private void Awake()
    {
        Log = Logger;
        ServerDiagnostics.Begin(Paths.BepInExRootPath, PluginVersion);
        harmony = new Harmony(PluginGuid);
        harmony.PatchAll();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded with Put Away, confirmed-kill, and George resident coordination.");
    }

    private void Update()
    {
        KillAttributionServer.Update();
        ResidentServer.Update();
    }

    private void OnDestroy()
    {
        KillAttributionServer.Reset();
        ResidentServer.Reset();
        InventoryTransactionRuntime.Shutdown();
        PutAwayLeaseServer.Reset();
        harmony?.UnpatchSelf();
        ServerDiagnostics.End();
    }
}
