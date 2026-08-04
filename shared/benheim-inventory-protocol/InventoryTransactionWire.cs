using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BenheimInventoryProtocol;

internal static class InventoryTransactionWire
{
    internal static string Hash(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(bytes);
        StringBuilder result = new StringBuilder(digest.Length * 2);
        foreach (byte value in digest)
        {
            result.Append(value.ToString("x2"));
        }

        return result.ToString();
    }

    internal static void WriteItem(ZPackage destination, ItemDrop.ItemData item)
    {
        Inventory inventory = new Inventory("benheim_wire", null, 1, 1);
        ItemDrop.ItemData clone = item.Clone();
        clone.m_gridPos = Vector2i.zero;
        inventory.AddItem(clone);
        ZPackage itemPackage = new ZPackage();
        inventory.Save(itemPackage);
        destination.Write(itemPackage);
    }

    internal static ItemDrop.ItemData? ReadItem(ZPackage source)
    {
        ZPackage itemPackage = source.ReadPackage();
        Inventory inventory = new Inventory("benheim_wire", null, 1, 1);
        inventory.Load(itemPackage);
        List<ItemDrop.ItemData> items = inventory.GetAllItems();
        return items.Count == 1 ? items[0] : null;
    }

    internal static ZPackage BuildResponse(
        string transactionId,
        string payloadHash,
        DepositStatus status,
        IReadOnlyList<int> accepted)
    {
        ZPackage response = new ZPackage();
        response.Write(InventoryTransactions.ProtocolVersion);
        response.Write(transactionId);
        response.Write(payloadHash);
        response.Write((int)status);
        response.Write(accepted.Count);
        foreach (int amount in accepted)
        {
            response.Write(amount);
        }

        return response;
    }

    internal static bool TryReadRequest(
        byte[] requestBytes,
        out int protocolVersion,
        out string transactionId,
        out long playerId,
        out ZDOID containerId,
        out List<RequestedDepositItem> items)
    {
        protocolVersion = 0;
        transactionId = string.Empty;
        playerId = 0L;
        containerId = ZDOID.None;
        items = new List<RequestedDepositItem>();
        try
        {
            ZPackage request = new ZPackage(requestBytes);
            protocolVersion = request.ReadInt();
            if (!InventoryTransactionRecoveryPolicy.CanReadRequest(protocolVersion))
            {
                return false;
            }

            transactionId = request.ReadString();
            playerId = request.ReadLong();
            containerId = request.ReadZDOID();
            int itemCount = request.ReadInt();
            if (transactionId.Length != 32
                || playerId == 0L
                || containerId.IsNone()
                || itemCount <= 0
                || itemCount > InventoryTransactions.MaxItemsPerDeposit)
            {
                return false;
            }

            for (int index = 0; index < itemCount; index++)
            {
                Vector2i position = request.ReadVector2i();
                ItemDrop.ItemData? item = ReadItem(request);
                if (item == null
                    || item.m_dropPrefab == null
                    || item.m_stack <= 0
                    || item.m_stack > item.m_shared.m_maxStackSize
                    || position.x < 0
                    || position.y < 0)
                {
                    return false;
                }

                items.Add(new RequestedDepositItem(item, position));
            }

            return request.GetPos() == request.Size();
        }
        catch (Exception)
        {
            protocolVersion = 0;
            transactionId = string.Empty;
            playerId = 0L;
            containerId = ZDOID.None;
            items.Clear();
            return false;
        }
    }

    internal static bool TryReadResponse(
        ZPackage package,
        out string transactionId,
        out string payloadHash,
        out DepositStatus status,
        out List<int> accepted)
    {
        transactionId = string.Empty;
        payloadHash = string.Empty;
        status = DepositStatus.InvalidRequest;
        accepted = new List<int>();
        try
        {
            if (package.ReadInt() != InventoryTransactions.ProtocolVersion)
            {
                status = DepositStatus.ProtocolMismatch;
                return false;
            }

            transactionId = package.ReadString();
            payloadHash = package.ReadString();
            status = (DepositStatus)package.ReadInt();
            int count = package.ReadInt();
            if (count < 0 || count > InventoryTransactions.MaxItemsPerDeposit)
            {
                status = DepositStatus.InvalidRequest;
                return false;
            }

            for (int index = 0; index < count; index++)
            {
                accepted.Add(package.ReadInt());
            }

            return true;
        }
        catch (Exception)
        {
            status = DepositStatus.InvalidRequest;
            return false;
        }
    }
}
