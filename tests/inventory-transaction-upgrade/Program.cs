using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BenheimInventoryProtocol;

const int LegacyProtocol = InventoryTransactionRecoveryPolicy.LegacyJournalProtocolVersion;
const int CurrentProtocol = InventoryTransactionRecoveryPolicy.CurrentProtocolVersion;
const long PlayerId = 9123456789L;
const long WorldId = 8123456789L;

Assert(CurrentProtocol == 2, "upgrade test no longer targets protocol 2");
Assert(InventoryTransactionRecoveryPolicy.CanReadRequest(LegacyProtocol), "protocol 1 journal request was rejected");
Assert(InventoryTransactionRecoveryPolicy.CanReadRequest(CurrentProtocol), "current request was rejected");
Assert(!InventoryTransactionRecoveryPolicy.CanReadRequest(0), "invalid protocol was accepted");
Assert(!InventoryTransactionRecoveryPolicy.CanReadRequest(3), "unknown future protocol was accepted");

AssertAction(PendingJournalPhase.Prepared, requestedCount: 2, acceptedCount: 0, PendingJournalRecoveryAction.RestorePrepared);
AssertAction(PendingJournalPhase.Reserved, requestedCount: 2, acceptedCount: 0, PendingJournalRecoveryAction.ResumeReserved);
AssertAction(PendingJournalPhase.Completed, requestedCount: 2, acceptedCount: 2, PendingJournalRecoveryAction.FinalizeCompleted);
AssertRejected(PendingJournalPhase.Prepared, 1, 1, "Prepared record carried accepted amounts");
AssertRejected(PendingJournalPhase.Reserved, 1, 1, "Reserved record carried accepted amounts");
AssertRejected(PendingJournalPhase.Completed, 2, 1, "Completed record had partial result shape");
AssertRejected(PendingJournalPhase.Completed, 0, 0, "empty request was accepted");

string temporaryRoot = Path.Combine(Path.GetTempPath(), "benheim-inventory-upgrade-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporaryRoot);
Paths.ConfigPath = temporaryRoot;

try
{
    Inventory source = new Inventory("player", null, 8, 4);
    RequestFixture prepared = BuildLegacyRequest(
        "11111111111111111111111111111111",
        new ZDOID(101L, 1U),
        (new Vector2i(0, 0), Item("$item_resin", 5)),
        (new Vector2i(1, 0), Item("$item_bonefragments", 4)));
    RequestFixture reserved = BuildLegacyRequest(
        "22222222222222222222222222222222",
        new ZDOID(102L, 2U),
        (new Vector2i(0, 1), Item("$item_stone", 7)),
        (new Vector2i(1, 1), Item("$item_wood", 6)));
    RequestFixture completed = BuildLegacyRequest(
        "33333333333333333333333333333333",
        new ZDOID(103L, 3U),
        (new Vector2i(0, 2), Item("$item_iron", 5)),
        (new Vector2i(1, 2), Item("$item_coins", 6)));

    InventoryTransactionJournal.WritePrepared(
        PlayerId,
        WorldId,
        prepared.TransactionId,
        prepared.PayloadHash,
        prepared.ContainerId,
        prepared.Bytes);
    InventoryTransactionJournal.WritePrepared(
        PlayerId,
        WorldId,
        reserved.TransactionId,
        reserved.PayloadHash,
        reserved.ContainerId,
        reserved.Bytes);
    InventoryTransactionJournal.MarkReserved(ToPending(reserved, source));
    InventoryTransactionJournal.WritePrepared(
        PlayerId,
        WorldId,
        completed.TransactionId,
        completed.PayloadHash,
        completed.ContainerId,
        completed.Bytes);
    InventoryTransactionJournal.MarkCompleted(ToPending(completed, source), new List<int> { 3, 2 });

    string journalDirectory = Path.Combine(
        temporaryRoot,
        "BenheimInventoryPending",
        WorldId.ToString(),
        PlayerId.ToString());
    string[] journalPaths = Directory.GetFiles(journalDirectory, "*.pending");
    Assert(journalPaths.Length == 3, "production journal writer did not persist three .pending records");
    Assert(journalPaths.All(path => Convert.FromBase64String(File.ReadAllText(path)).Length > 0), "journal was not persisted as serialized base64 data");

    List<PendingJournalRecord> serialized = InventoryTransactionJournal.ReadAll(PlayerId, WorldId);
    Assert(serialized.Count == 3, "production journal reader did not deserialize every record");
    Assert(Record(serialized, prepared).Phase == PendingJournalPhase.Prepared, "Prepared journal phase changed on disk");
    Assert(Record(serialized, reserved).Phase == PendingJournalPhase.Reserved, "Reserved journal phase changed on disk");
    Assert(Record(serialized, completed).Phase == PendingJournalPhase.Completed, "Completed journal phase changed on disk");
    Assert(Record(serialized, completed).Accepted.SequenceEqual(new[] { 3, 2 }), "Completed accepted amounts changed on disk");
    Assert(Record(serialized, reserved).RequestBytes.SequenceEqual(reserved.Bytes), "Reserved request bytes changed on disk");

    AssertParsedLegacyRequest(prepared);
    AssertParsedLegacyRequest(reserved);
    AssertParsedLegacyRequest(completed);
    AssertReceiptIdentityUsesOriginalRequest(reserved);

    // This reset is the process boundary: only the serialized journals and the
    // character inventory survive before protocol 2 runs recovery.
    InventoryTransactions.TestReset();
    source.AddItem(Item("$item_resin", 2), new Vector2i(0, 0));
    source.AddItem(Item("$item_stone", 7), new Vector2i(0, 1));
    source.AddItem(Item("$item_wood", 6), new Vector2i(1, 1));
    source.AddItem(Item("$item_iron", 5), new Vector2i(0, 2));
    source.AddItem(Item("$item_coins", 1), new Vector2i(1, 2));
    Player.m_localPlayer = new Player(source);
    Game.instance = new Game();
    Game.instance.Profile.PlayerId = PlayerId;
    ZNet.instance.WorldId = WorldId;

    InventoryTransactions.TestRecover();

    Assert(InventoryTransactions.TestWarnings.Count == 0, "valid legacy journals emitted recovery warnings");

    Assert(InventoryTransactions.TestIsCompleted(prepared.TransactionId), "Prepared journal did not route to completed rollback");
    Assert(!InventoryTransactions.TestIsPending(prepared.TransactionId), "Prepared journal was incorrectly retried");
    AssertStack(source, new Vector2i(0, 0), "$item_resin", 5, "Prepared partial stack was not restored");
    AssertStack(source, new Vector2i(1, 0), "$item_bonefragments", 4, "Prepared missing stack was not restored");

    Assert(InventoryTransactions.TestIsPending(reserved.TransactionId), "Reserved journal did not route to retry");
    PendingDeposit retried = InventoryTransactions.TestPending(reserved.TransactionId);
    Assert(retried.RequestBytes.SequenceEqual(reserved.Bytes), "Reserved recovery rewrote legacy request bytes");
    Assert(retried.PayloadHash == reserved.PayloadHash, "Reserved recovery changed the legacy payload hash");
    Assert(source.GetItemAt(0, 1) == null && source.GetItemAt(1, 1) == null, "Reserved items were not removed before retry");
    Assert(InventoryTransactions.TestSentRequests.Count == 1, "Reserved recovery did not route exactly one retry");
    Assert(InventoryTransactions.TestSentRequests[0].SequenceEqual(reserved.Bytes), "Retry did not send the exact protocol-1 request bytes");

    Assert(InventoryTransactions.TestIsCompleted(completed.TransactionId), "Completed journal did not route to normalization");
    Assert(!InventoryTransactions.TestIsPending(completed.TransactionId), "Completed journal was incorrectly retried");
    AssertStack(source, new Vector2i(0, 2), "$item_iron", 2, "Completed excess stack was not reduced to the unaccepted remainder");
    AssertStack(source, new Vector2i(1, 2), "$item_coins", 4, "Completed missing remainder was not restored");

    Console.WriteLine("inventory transaction upgrade tests passed");
}
finally
{
    Player.m_localPlayer = null!;
    Game.instance = null!;
    Directory.Delete(temporaryRoot, recursive: true);
}

static RequestFixture BuildLegacyRequest(
    string transactionId,
    ZDOID containerId,
    params (Vector2i Position, ItemDrop.ItemData Item)[] items)
{
    ZPackage request = new ZPackage();
    request.Write(LegacyProtocol);
    request.Write(transactionId);
    request.Write(PlayerId);
    request.Write(containerId);
    request.Write(items.Length);
    foreach ((Vector2i position, ItemDrop.ItemData item) in items)
    {
        request.Write(position);
        InventoryTransactionWire.WriteItem(request, item);
    }

    byte[] bytes = request.GetArray();
    return new RequestFixture(
        transactionId,
        containerId,
        bytes,
        InventoryTransactionWire.Hash(bytes),
        items);
}

static PendingDeposit ToPending(RequestFixture fixture, Inventory source)
{
    return new PendingDeposit(
        fixture.TransactionId,
        fixture.PayloadHash,
        fixture.ContainerId,
        fixture.Bytes,
        PlayerId,
        WorldId,
        source,
        fixture.Items
            .Select(entry => new ReservedDepositItem(entry.Item.Clone(), entry.Position))
            .ToList(),
        _ => { },
        0f);
}

static PendingJournalRecord Record(List<PendingJournalRecord> records, RequestFixture fixture)
{
    return records.Single(record => record.TransactionId == fixture.TransactionId);
}

static void AssertReceiptIdentityUsesOriginalRequest(RequestFixture fixture)
{
    string ledger = InventoryTransactionReceiptCodec.Record(
        string.Empty,
        fixture.TransactionId,
        fixture.PayloadHash,
        status: 0,
        new[] { 7, 6 });
    Assert(
        InventoryTransactionReceiptCodec.TryRead(
            ledger,
            fixture.TransactionId,
            fixture.PayloadHash,
            out bool conflict,
            out TransactionReceipt? receipt)
        && !conflict
        && receipt != null
        && receipt.Accepted.SequenceEqual(new[] { 7, 6 }),
        "original protocol-1 request did not resolve its committed receipt");

    byte[] rewritten = (byte[])fixture.Bytes.Clone();
    rewritten[0] = CurrentProtocol;
    string rewrittenHash = InventoryTransactionWire.Hash(rewritten);
    Assert(
        InventoryTransactionReceiptCodec.TryRead(
            ledger,
            fixture.TransactionId,
            rewrittenHash,
            out conflict,
            out receipt)
        && conflict,
        "rewritten protocol-1 request did not conflict with its committed receipt");
    Assert(
        InventoryTransactionReceiptCodec.TryRead(
            InventoryTransactionReceiptCodec.Remove(
                ledger,
                fixture.TransactionId,
                rewrittenHash),
            fixture.TransactionId,
            fixture.PayloadHash,
            out conflict,
            out receipt),
        "receipt was removed without the original protocol-1 request hash");
}

static void AssertParsedLegacyRequest(RequestFixture fixture)
{
    Assert(
        InventoryTransactionWire.TryReadRequest(
            fixture.Bytes,
            out int protocol,
            out string transactionId,
            out long playerId,
            out ZDOID containerId,
            out List<RequestedDepositItem> items),
        $"production wire parser rejected {fixture.TransactionId}");
    Assert(protocol == LegacyProtocol, "wire parser did not preserve protocol 1");
    Assert(transactionId == fixture.TransactionId, "wire parser changed transaction id");
    Assert(playerId == PlayerId, "wire parser changed player id");
    Assert(containerId == fixture.ContainerId, "wire parser changed container id");
    Assert(items.Count == fixture.Items.Length, "wire parser changed item count");
    for (int index = 0; index < items.Count; index++)
    {
        Assert(items[index].SourcePosition.Equals(fixture.Items[index].Position), "wire parser changed source position");
        Assert(items[index].Item.m_shared.m_name == fixture.Items[index].Item.m_shared.m_name, "wire parser changed item type");
        Assert(items[index].Item.m_stack == fixture.Items[index].Item.m_stack, "wire parser changed item stack");
    }
}

static ItemDrop.ItemData Item(string name, int stack)
{
    return new ItemDrop.ItemData
    {
        m_shared = new ItemDrop.SharedData
        {
            m_name = name,
            m_maxStackSize = 50,
        },
        m_dropPrefab = new object(),
        m_stack = stack,
        m_quality = 1,
        m_worldLevel = 0,
    };
}

static void AssertStack(Inventory inventory, Vector2i position, string name, int expected, string message)
{
    ItemDrop.ItemData? item = inventory.GetItemAt(position.x, position.y);
    Assert(item != null && item.m_shared.m_name == name && item.m_stack == expected, message);
}

static void AssertAction(
    PendingJournalPhase phase,
    int requestedCount,
    int acceptedCount,
    PendingJournalRecoveryAction expected)
{
    Assert(
        InventoryTransactionRecoveryPolicy.TryChooseAction(
            LegacyProtocol,
            phase,
            requestedCount,
            acceptedCount,
            out PendingJournalRecoveryAction actual),
        $"protocol 1/{phase} did not produce a recovery action");
    Assert(actual == expected, $"protocol 1/{phase} chose {actual}, expected {expected}");
}

static void AssertRejected(
    PendingJournalPhase phase,
    int requestedCount,
    int acceptedCount,
    string message)
{
    Assert(
        !InventoryTransactionRecoveryPolicy.TryChooseAction(
            LegacyProtocol,
            phase,
            requestedCount,
            acceptedCount,
            out PendingJournalRecoveryAction action)
        && action == PendingJournalRecoveryAction.None,
        message);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class RequestFixture
{
    internal RequestFixture(
        string transactionId,
        ZDOID containerId,
        byte[] bytes,
        string payloadHash,
        (Vector2i Position, ItemDrop.ItemData Item)[] items)
    {
        TransactionId = transactionId;
        ContainerId = containerId;
        Bytes = bytes;
        PayloadHash = payloadHash;
        Items = items;
    }

    internal string TransactionId { get; }
    internal ZDOID ContainerId { get; }
    internal byte[] Bytes { get; }
    internal string PayloadHash { get; }
    internal (Vector2i Position, ItemDrop.ItemData Item)[] Items { get; }
}
