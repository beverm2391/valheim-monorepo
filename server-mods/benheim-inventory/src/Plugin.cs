using BepInEx;
using BenheimInventoryProtocol;

namespace BenheimInventory;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.benheim.inventory";
    public const string PluginName = "Benheim Inventory";
    public const string PluginVersion = "0.1.2";
    public const string LoadMessage = "Benheim Inventory 0.1.2 loaded with protocol 2.";

    private void Awake()
    {
        InventoryTransactions.Initialize(Logger, PluginVersion);
        Logger.LogInfo(LoadMessage);
    }

    private void Update()
    {
        InventoryTransactions.Update();
    }

    private void OnDestroy()
    {
        InventoryTransactions.Shutdown();
    }
}
