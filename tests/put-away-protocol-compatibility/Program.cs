using BenheimInventoryProtocol;
using BenheimQoL.InventoryFeature;

const string LegacyLeaseRequestRpc = "Benheim.PutAway.Lease.Request.v1";
const string LegacyLeaseResultRpc = "Benheim.PutAway.Lease.Result.v1";
const string LegacyLeaseReleaseRpc = "Benheim.PutAway.Lease.Release.v1";

ProtocolGeneration legacy = new(
    3,
    LegacyLeaseRequestRpc,
    LegacyLeaseResultRpc,
    LegacyLeaseReleaseRpc,
    InventoryRpcNames(3));
ProtocolGeneration current = new(
    InventoryTransactionProtocol.Version,
    PutAwayLeaseProtocol.RequestRpc,
    PutAwayLeaseProtocol.ResultRpc,
    PutAwayLeaseProtocol.ReleaseRpc,
    new HashSet<string>(StringComparer.Ordinal)
    {
        InventoryTransactionProtocol.DepositRequestRpc,
        InventoryTransactionProtocol.OwnerExecuteRpc,
        InventoryTransactionProtocol.OwnerResultRpc,
        InventoryTransactionProtocol.DepositResultRpc,
        InventoryTransactionProtocol.ReceiptAckRpc,
        InventoryTransactionProtocol.OwnerReceiptAckRpc,
    });

Expect(current.InventoryVersion == 4, "the corrected inventory payload generation must be v4");
Expect(
    legacy.InventoryRpcNames.All(name => !current.InventoryRpcNames.Contains(name)),
    "v3 and v4 inventory generations unexpectedly share an RPC surface");
Expect(
    LeaseRpcNames(legacy).All(name => !LeaseRpcNames(current).Contains(name)),
    "v1 and v2 lease generations unexpectedly share an RPC surface");

PutAwayAttempt sameGeneration = Simulate(current, current);
Expect(
    sameGeneration.LeaseGranted
    && sameGeneration.Scanned
    && sameGeneration.Reserved
    && sameGeneration.InventoryRequestHandled,
    "same-generation control did not enter the owner-routed flow");
PutAwayAttempt legacySameGeneration = Simulate(legacy, legacy);
Expect(
    legacySameGeneration.LeaseGranted
    && legacySameGeneration.Scanned
    && legacySameGeneration.Reserved
    && legacySameGeneration.InventoryRequestHandled,
    "legacy same-generation control did not enter its owner-routed flow");

PutAwayAttempt newClientOldServer = Simulate(current, legacy);
Expect(
    !newClientOldServer.LeaseGranted
    && !newClientOldServer.Scanned
    && !newClientOldServer.Reserved
    && !newClientOldServer.InventoryRequestHandled,
    "v4 client with v3 server entered scanning or reservation");

PutAwayAttempt oldClientNewServer = Simulate(legacy, current);
Expect(
    !oldClientNewServer.LeaseGranted
    && !oldClientNewServer.Scanned
    && !oldClientNewServer.Reserved
    && !oldClientNewServer.InventoryRequestHandled,
    "v3 client with v4 server entered scanning or reservation");

Console.WriteLine("Put Away mixed-version pre-reservation compatibility checks passed");

static PutAwayAttempt Simulate(ProtocolGeneration client, ProtocolGeneration server)
{
    // Unknown ZRpc method hashes are ignored. The server can grant only when
    // it registered the exact request name, and the client can enter scanning
    // only when it registered the exact result name used by that server.
    bool serverHandledLeaseRequest =
        string.Equals(client.LeaseRequestRpc, server.LeaseRequestRpc, StringComparison.Ordinal);
    bool clientHandledLeaseResult = serverHandledLeaseRequest
        && string.Equals(server.LeaseResultRpc, client.LeaseResultRpc, StringComparison.Ordinal);
    bool leaseGranted = clientHandledLeaseResult;

    bool scanned = leaseGranted;
    bool reserved = scanned;
    string inventoryRequest = client.InventoryRpcNames.Single(
        name => name.EndsWith(".DepositRequest", StringComparison.Ordinal));
    bool inventoryRequestHandled =
        reserved && server.InventoryRpcNames.Contains(inventoryRequest);
    return new PutAwayAttempt(
        leaseGranted,
        scanned,
        reserved,
        inventoryRequestHandled);
}

static HashSet<string> InventoryRpcNames(int version) =>
    new(StringComparer.Ordinal)
    {
        $"Benheim.Inventory.v{version}.DepositRequest",
        $"Benheim.Inventory.v{version}.OwnerExecute",
        $"Benheim.Inventory.v{version}.OwnerResult",
        $"Benheim.Inventory.v{version}.DepositResult",
        $"Benheim.Inventory.v{version}.ReceiptAck",
        $"Benheim.Inventory.v{version}.OwnerReceiptAck",
    };

static HashSet<string> LeaseRpcNames(ProtocolGeneration generation) =>
    new(StringComparer.Ordinal)
    {
        generation.LeaseRequestRpc,
        generation.LeaseResultRpc,
        generation.LeaseReleaseRpc,
    };

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed record ProtocolGeneration(
    int InventoryVersion,
    string LeaseRequestRpc,
    string LeaseResultRpc,
    string LeaseReleaseRpc,
    HashSet<string> InventoryRpcNames);

internal sealed record PutAwayAttempt(
    bool LeaseGranted,
    bool Scanned,
    bool Reserved,
    bool InventoryRequestHandled);
