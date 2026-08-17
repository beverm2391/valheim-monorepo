using BenheimInventoryProtocol;
using HarmonyLib;

namespace BenheimServerSupport;

/// <summary>
/// Hosts the server routing half of Put Away's shared owner-authoritative
/// protocol. The global lease remains a separate pre-scan exclusion rule.
/// </summary>
[HarmonyPatch]
internal static class InventoryTransactionRuntime
{
    private static bool initialized;

    [HarmonyPatch(typeof(ZNet), "Awake")]
    [HarmonyPostfix]
    private static void AfterNetworkAwake()
    {
        EnsureInitialized();
    }

    [HarmonyPatch(typeof(ZNet), "Update")]
    [HarmonyPostfix]
    private static void AfterNetworkUpdate()
    {
        EnsureInitialized();
        InventoryTransactions.Update();
    }

    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    [HarmonyPrefix]
    private static void BeforeNetworkDestroy()
    {
        Shutdown();
    }

    internal static void Shutdown()
    {
        if (!initialized)
        {
            return;
        }

        InventoryTransactions.Shutdown();
        initialized = false;
    }

    private static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        InventoryTransactions.Initialize(
            new InventoryTransactionDiagnosticSink(Plugin.Log),
            Plugin.PluginVersion);
        initialized = true;
    }
}
