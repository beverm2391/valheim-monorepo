using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;

namespace BenheimInventoryProtocol;

internal sealed class PendingJournalRecord
{
    internal PendingJournalRecord(
        PendingJournalPhase phase,
        long playerId,
        long worldId,
        string transactionId,
        string payloadHash,
        ZDOID containerId,
        byte[] requestBytes,
        List<int> accepted)
    {
        Phase = phase;
        PlayerId = playerId;
        WorldId = worldId;
        TransactionId = transactionId;
        PayloadHash = payloadHash;
        ContainerId = containerId;
        RequestBytes = requestBytes;
        Accepted = accepted;
    }

    internal PendingJournalPhase Phase { get; }
    internal long PlayerId { get; }
    internal long WorldId { get; }
    internal string TransactionId { get; }
    internal string PayloadHash { get; }
    internal ZDOID ContainerId { get; }
    internal byte[] RequestBytes { get; }
    internal List<int> Accepted { get; }
}

internal static class InventoryTransactionJournal
{
    private const int JournalVersion = 2;
    private static string RootPath => Path.Combine(Paths.ConfigPath, "BenheimInventoryPending");

    internal static void WritePrepared(
        long playerId,
        long worldId,
        string transactionId,
        string payloadHash,
        ZDOID containerId,
        byte[] requestBytes)
    {
        Write(new PendingJournalRecord(
            PendingJournalPhase.Prepared,
            playerId,
            worldId,
            transactionId,
            payloadHash,
            containerId,
            requestBytes,
            new List<int>()));
    }

    internal static void MarkReserved(PendingDeposit pending)
    {
        Write(FromPending(pending, PendingJournalPhase.Reserved, new List<int>()));
    }

    internal static void MarkCompleted(PendingDeposit pending, List<int> accepted)
    {
        Write(FromPending(pending, PendingJournalPhase.Completed, accepted));
    }

    internal static List<PendingJournalRecord> ReadAll(long playerId, long worldId)
    {
        List<PendingJournalRecord> records = new List<PendingJournalRecord>();
        string directory = GetDirectory(playerId, worldId);
        if (!Directory.Exists(directory))
        {
            return records;
        }

        foreach (string path in Directory.GetFiles(directory, "*.pending"))
        {
            try
            {
                PendingJournalRecord? record = Read(path);
                if (record != null
                    && record.PlayerId == playerId
                    && record.WorldId == worldId)
                {
                    records.Add(record);
                }
            }
            catch (Exception ex)
            {
                InventoryTransactions.LogWarning(
                    $"journal_read_failed file=\"{Path.GetFileName(path)}\" error=\"{ex.Message}\"");
            }
        }

        return records;
    }

    internal static void Delete(PendingDeposit pending)
    {
        Delete(pending.TransactionId, pending.PlayerId, pending.WorldId);
    }

    internal static void Delete(string transactionId, long playerId, long worldId)
    {
        string path = GetPath(transactionId, playerId, worldId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static PendingJournalRecord FromPending(
        PendingDeposit pending,
        PendingJournalPhase phase,
        List<int> accepted)
    {
        return new PendingJournalRecord(
            phase,
            pending.PlayerId,
            pending.WorldId,
            pending.TransactionId,
            pending.PayloadHash,
            pending.ContainerId,
            pending.RequestBytes,
            accepted);
    }

    private static PendingJournalRecord? Read(string path)
    {
        ZPackage package = new ZPackage(File.ReadAllText(path));
        if (package.ReadInt() != JournalVersion)
        {
            return null;
        }

        PendingJournalPhase phase = (PendingJournalPhase)package.ReadInt();
        long playerId = package.ReadLong();
        long worldId = package.ReadLong();
        string transactionId = package.ReadString();
        string payloadHash = package.ReadString();
        ZDOID containerId = package.ReadZDOID();
        byte[] requestBytes = package.ReadByteArray();
        int acceptedCount = package.ReadInt();
        if (acceptedCount < 0 || acceptedCount > InventoryTransactions.MaxItemsPerDeposit)
        {
            return null;
        }

        List<int> accepted = new List<int>(acceptedCount);
        for (int index = 0; index < acceptedCount; index++)
        {
            accepted.Add(package.ReadInt());
        }

        if (!Enum.IsDefined(typeof(PendingJournalPhase), phase)
            || playerId == 0L
            || worldId == 0L
            || transactionId.Length != 32
            || payloadHash != InventoryTransactionWire.Hash(requestBytes)
            || package.GetPos() != package.Size())
        {
            return null;
        }

        return new PendingJournalRecord(
            phase,
            playerId,
            worldId,
            transactionId,
            payloadHash,
            containerId,
            requestBytes,
            accepted);
    }

    private static void Write(PendingJournalRecord record)
    {
        string directory = GetDirectory(record.PlayerId, record.WorldId);
        Directory.CreateDirectory(directory);
        ZPackage package = new ZPackage();
        package.Write(JournalVersion);
        package.Write((int)record.Phase);
        package.Write(record.PlayerId);
        package.Write(record.WorldId);
        package.Write(record.TransactionId);
        package.Write(record.PayloadHash);
        package.Write(record.ContainerId);
        package.Write(record.RequestBytes);
        package.Write(record.Accepted.Count);
        foreach (int amount in record.Accepted)
        {
            package.Write(amount);
        }

        string path = GetPath(record.TransactionId, record.PlayerId, record.WorldId);
        string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        byte[] bytes = Encoding.UTF8.GetBytes(package.GetBase64());
        using (FileStream stream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
        {
            File.Replace(temporaryPath, path, null);
        }
        else
        {
            File.Move(temporaryPath, path);
        }
    }

    private static string GetDirectory(long playerId, long worldId)
    {
        return Path.Combine(RootPath, worldId.ToString(), playerId.ToString());
    }

    private static string GetPath(string transactionId, long playerId, long worldId)
    {
        return Path.Combine(GetDirectory(playerId, worldId), transactionId + ".pending");
    }
}
