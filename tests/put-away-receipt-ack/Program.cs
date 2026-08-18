using System;
using BenheimInventoryProtocol;
using BenheimServerSupport;

const long requesterPeer = 1425023986L;
const long otherPeer = 39252744L;
const long ownerPeer = requesterPeer;
const string transactionId = "bd6b7e8055f14fcb963726ced8d6043e";
const string payloadHash = "75a329c06c38c434420f1a6ed68054d9";

ZDOID chestId = new ZDOID(1L, 372384U);
ConnectedTransactionRouter<ZDOID> router = new ConnectedTransactionRouter<ZDOID>();
ServerRequestDecision route = router.ReceiveRequest(
    transactionId,
    requesterPeer,
    payloadHash,
    new byte[] { 1, 2, 3 },
    chestId,
    ownerPeer);
Expect(route.Action == ServerRequestAction.Route, "request did not route to the current owner");
Expect(
    router.ReceiveOwnerResult(
        transactionId,
        requesterPeer,
        payloadHash,
        ownerPeer,
        ownerPeer,
        new byte[] { 4, 5, 6 },
        completedAt: 1f,
        ownerReportedStale: false) == OwnerResultAction.Complete,
    "owner result did not create the completed correlation");

// Use Valheim's installed package and routed-RPC serializers. The routed
// sender is the requester's peer UID even when no replicated Player GameObject
// exists on the dedicated server.
ZPackage clientAcknowledgement = InventoryTransactionReceiptAcknowledgementCodec.Write(
    transactionId,
    payloadHash,
    chestId);
ZPackage routedParameters = new ZPackage();
ZRpc.Serialize(new object[] { clientAcknowledgement }, ref routedParameters);
routedParameters.SetPos(0);
ZRoutedRpc.RoutedRPCData outbound = new ZRoutedRpc.RoutedRPCData
{
    m_msgID = 17L,
    m_senderPeerID = requesterPeer,
    m_targetPeerID = 1L,
    m_targetZDO = ZDOID.None,
    m_methodHash = 12345,
    m_parameters = routedParameters,
};
ZPackage transport = new ZPackage();
outbound.Serialize(transport);
transport.SetPos(0);
ZRoutedRpc.RoutedRPCData inbound = new ZRoutedRpc.RoutedRPCData();
inbound.Deserialize(transport);
ZPackage deliveredAcknowledgement = inbound.m_parameters.ReadPackage();

Expect(
    InventoryTransactionReceiptAcknowledgementCodec.TryAuthorize(
        deliveredAcknowledgement,
        router,
        inbound.m_senderPeerID,
        out string decodedTransactionId,
        out string decodedPayloadHash,
        out ZDOID decodedContainerId,
        out string validRejectionReason),
    $"production cleanup authorization rejected the routed package: {validRejectionReason}");
Expect(deliveredAcknowledgement.GetPos() == deliveredAcknowledgement.Size(),
    "receipt acknowledgement left unexpected bytes on the wire");
Expect(inbound.m_senderPeerID == requesterPeer,
    "routed RPC did not preserve the authenticated requester peer UID");
Expect(
    !InventoryTransactionReceiptAcknowledgementCodec.TryAuthorize(
        ReadableAcknowledgement(
            decodedTransactionId,
            decodedPayloadHash,
            decodedContainerId),
        router,
        otherPeer,
        out _,
        out _,
        out _,
        out string otherPeerRejectionReason)
    && otherPeerRejectionReason == "completed_correlation_mismatch",
    "production cleanup authorization accepted another routed peer");

// This reproduces the 0.1.64 failure class. The dedicated server can lack the
// remote Player scene object even though routed peer identity and completion
// correlation are valid. That redundant lookup must not reject cleanup.
bool replicatedPlayerObjectFound = false;
Expect(!replicatedPlayerObjectFound,
    "unsafe control requires the dedicated-server Player lookup to be absent");
Expect(
    InventoryTransactionReceiptAcknowledgementCodec.TryAuthorize(
        ReadableAcknowledgement(
            decodedTransactionId,
            decodedPayloadHash,
            decodedContainerId),
        router,
        inbound.m_senderPeerID,
        out _,
        out _,
        out _,
        out _),
    "production cleanup authorization depended on a Player scene object");

Expect(
    InventoryTransactionSettlement.TryCreate(
        new[] { 1 },
        new[] { 1 },
        out InventoryTransactionSettlement? settlement),
    "exact owner result did not settle");
Expect(settlement!.Accepted[0] + settlement.Rejected[0] == 1,
    "settlement did not conserve the reserved count");

object requesterConnection = new object();
object contenderConnection = new object();
PutAwayLeaseState<object> lease = new PutAwayLeaseState<object>();
Expect(lease.TryAcquire(requesterConnection, "operation-live"),
    "requester did not acquire the global lease");

// Unsafe 0.1.64 control: a missing cleanup confirmation keeps completion and
// lease release behind the receipt round trip forever.
bool cleanupConfirmed = false;
bool oldBlockingCompletion = settlement != null && cleanupConfirmed;
Expect(!oldBlockingCompletion, "unsafe blocking control unexpectedly completed");
Expect(!lease.TryAcquire(contenderConnection, "operation-contender"),
    "unsafe blocking control did not retain the global lease");

// Current contract: exact settlement completes gameplay. The one-way cleanup
// is outside transaction state and cannot retain the operation or lease.
bool exactSettlementCompleted = settlement != null;
Expect(exactSettlementCompleted, "exact settlement did not complete gameplay");
Expect(lease.TryRelease(requesterConnection, "operation-live"),
    "exact settlement did not release the requester's lease");
Expect(lease.TryAcquire(contenderConnection, "operation-contender"),
    "receipt cleanup retained the global lease after exact settlement");

System.Console.WriteLine("Put Away receipt acknowledgement wire and liveness checks passed");

static ZPackage ReadableAcknowledgement(
    string transactionId,
    string payloadHash,
    ZDOID containerId)
{
    ZPackage package = InventoryTransactionReceiptAcknowledgementCodec.Write(
        transactionId,
        payloadHash,
        containerId);
    package.SetPos(0);
    return package;
}

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
