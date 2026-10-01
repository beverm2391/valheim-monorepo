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

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
    [HarmonyPrefix]
    private static void BeforeNetworkShutdown()
    {
        // Logout shuts networking down before Unity destroys ZNet. Drop safe
        // predeposit state at that boundary, while the server RPC still exists.
        QuickStack.ResetState();
    }

    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    [HarmonyPrefix]
    private static void BeforeNetworkDestroy()
    {
        // Cover direct destruction too, even if transaction initialization did
        // not run. Check the unsettled-deposit guard BEFORE
        // Shutdown clears protocol state; reconnect recovery is unsupported.
        QuickStack.ResetState();
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
            InventoryTransactionDiagnosticSink.Instance,
            Plugin.PluginVersion);
        initialized = true;
    }
}
