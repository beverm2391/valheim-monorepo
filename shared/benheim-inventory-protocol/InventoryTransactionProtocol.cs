namespace BenheimInventoryProtocol;

/// <summary>
/// Owns the wire generation shared by the requester, server router, and chest
/// owner. A generation change uses entirely new RPC names so mixed peers never
/// enter reservation or mutation through an older message surface.
/// </summary>
internal static class InventoryTransactionProtocol
{
    internal const int Version = 4;
    internal const string DepositRequestRpc = "Benheim.Inventory.v4.DepositRequest";
    internal const string OwnerExecuteRpc = "Benheim.Inventory.v4.OwnerExecute";
    internal const string OwnerResultRpc = "Benheim.Inventory.v4.OwnerResult";
    internal const string DepositResultRpc = "Benheim.Inventory.v4.DepositResult";
    internal const string ReceiptAckRpc = "Benheim.Inventory.v4.ReceiptAck";
    internal const string OwnerReceiptAckRpc = "Benheim.Inventory.v4.OwnerReceiptAck";
}
