using BenheimInventoryProtocol;
using HarmonyLib;

namespace BenheimQoL.InventoryFeature;

/// <summary>
/// Composition root for Put Away's owner-authoritative transaction protocol.
/// Read shared/benheim-inventory-protocol/PROTOCOL.md before changing this
/// lifecycle or replacing the protocol with a locally simpler write path.
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

        InventoryTransactions.Initialize(Plugin.Log, Plugin.PluginVersion);
        initialized = true;
    }
}
