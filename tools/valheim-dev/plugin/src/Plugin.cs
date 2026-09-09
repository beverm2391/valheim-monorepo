using System;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ValheimDev;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.benheim.qol", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.beneverman.valheim-dev";
    public const string PluginName = "Valheim Dev";
    public const string PluginVersion = "0.3.0";

    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony? harmony;

    private void Awake()
    {
        Log = Logger;
        string configuredRoot = Environment.GetEnvironmentVariable("VALHEIM_DEV_ROOT") ?? string.Empty;
        string dataRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? System.IO.Path.Combine(Paths.BepInExRootPath, "ValheimDev")
            : configuredRoot;
        ValheimDevRuntime.Initialize(
            dataRoot,
            System.IO.Path.Combine(Paths.BepInExRootPath, "LogOutput.log"),
            PluginVersion,
            Thread.CurrentThread.ManagedThreadId);
        ValheimDevConsole.Initialize();

        try
        {
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll();
        }
        catch (Exception exception)
        {
            Logger.LogWarning("Valheim Dev could not install its world-teardown patch: "
                + ValheimDevDiagnostics.Flatten(exception.Message));
        }

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Data root: {ValheimDevRuntime.DataRoot}");
    }

    private void Update()
    {
        ValheimDevRuntime.Update();
    }

    private void OnDestroy()
    {
        ValheimDevRuntime.Revoke("plugin_teardown");
        ValheimDevConsole.Reset();
        try { harmony?.UnpatchSelf(); }
        catch (Exception exception)
        {
            Logger.LogWarning("Valheim Dev could not remove its Harmony patches: "
                + ValheimDevDiagnostics.Flatten(exception.Message));
        }
        harmony = null;
    }
}
