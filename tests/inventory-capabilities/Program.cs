using System;
using BenheimInventoryProtocol;

const int LocalProtocol = 2;
InventoryPeerCapabilityRegistry registry = new();
object firstConnection = new();
object replacementConnection = new();
registry.Record(42L, firstConnection, LocalProtocol, "0.1.39");
Assert(
    registry.TryGet(42L, firstConnection, out InventoryPeerAdvertisement firstAdvertisement)
        && firstAdvertisement.ProtocolVersion == LocalProtocol,
    "current connection lost its capability hello");
Assert(
    !registry.TryGet(42L, replacementConnection, out _),
    "replacement connection inherited the prior connection's compatible hello");
registry.RemoveStaleConnections(new[] { replacementConnection });
Assert(
    !registry.TryGet(42L, replacementConnection, out _),
    "stale compatible state survived current-peer replacement");
registry.Record(42L, replacementConnection, LocalProtocol, "0.1.39");
Assert(
    registry.TryGet(42L, replacementConnection, out _),
    "replacement connection was not compatible after its own hello");

InventoryCapabilitySnapshot mismatchedRoster = Snapshot(
    "0.1.2",
    2,
    Peer("Alice", "0.1.39", 2, detected: true, compatible: true),
    Peer("Bob", "9.4.0", 1, detected: true, compatible: false));
InventoryCapabilitySnapshot semanticVersionChange = Snapshot(
    "8.0.0",
    2,
    Peer("Alice", "5.6.7", 2, detected: true, compatible: true),
    Peer("Bob", "10.0.0", 1, detected: true, compatible: false));

Assert(
    mismatchedRoster.GetIncompatibilityKey(LocalProtocol)
        == semanticVersionChange.GetIncompatibilityKey(LocalProtocol),
    "semantic versions changed the Put Away safety key");
Assert(
    mismatchedRoster.GetDisplayFingerprint()
        != semanticVersionChange.GetDisplayFingerprint(),
    "semantic versions were lost from diagnostic status");

InventoryCapabilitySnapshot missingClient = Snapshot(
    "0.1.2",
    2,
    Peer("Vanilla", string.Empty, 0, detected: false, compatible: false));
Assert(
    missingClient.GetIncompatibilityKey(LocalProtocol)
        != mismatchedRoster.GetIncompatibilityKey(LocalProtocol),
    "missing and mismatched clients produced the same roster identity");

InventoryCompatibilityWarningTracker tracker = new();
Assert(!tracker.TryGetWarningKey(mismatchedRoster, LocalProtocol, 0f, 8f, out _), "warning ignored grace start");
Assert(!tracker.TryGetWarningKey(mismatchedRoster, LocalProtocol, 7.9f, 8f, out _), "warning ignored grace duration");
Assert(tracker.TryGetWarningKey(mismatchedRoster, LocalProtocol, 8f, 8f, out string firstKey), "warning was not ready after grace");
tracker.MarkWarned(firstKey);
Assert(!tracker.TryGetWarningKey(semanticVersionChange, LocalProtocol, 9f, 8f, out _), "semantic version text repeated the warning");
Assert(!tracker.TryGetWarningKey(missingClient, LocalProtocol, 9f, 8f, out _), "changed roster skipped its handshake grace");
Assert(!tracker.TryGetWarningKey(missingClient, LocalProtocol, 16.9f, 8f, out _), "changed roster used a shortened handshake grace");
Assert(tracker.TryGetWarningKey(missingClient, LocalProtocol, 17f, 8f, out string changedKey), "changed roster did not re-warn after grace");
tracker.MarkWarned(changedKey);
Assert(!tracker.TryGetWarningKey(missingClient, LocalProtocol, 18f, 8f, out _), "unchanged roster repeated the warning");

InventoryCapabilitySnapshot compatibleRoster = new(
    InventoryCapabilityState.Ready,
    "0.1.2",
    2,
    new[] { Peer("Vanilla", "0.1.39", 2, detected: true, compatible: true) });
Assert(!tracker.TryGetWarningKey(compatibleRoster, LocalProtocol, 19f, 8f, out _), "compatible roster produced a warning");
Assert(!tracker.TryGetWarningKey(missingClient, LocalProtocol, 20f, 8f, out _), "new incompatibility episode skipped its grace");
Assert(tracker.TryGetWarningKey(missingClient, LocalProtocol, 28f, 8f, out _), "new incompatibility episode did not warn after grace");

Assert(!tracker.TryGetWarningKey(InventoryCapabilitySnapshot.Disconnected, LocalProtocol, 29f, 8f, out _), "disconnect produced a warning");
Assert(!tracker.TryGetWarningKey(missingClient, LocalProtocol, 30f, 8f, out _), "reconnect ignored a fresh grace period");
Assert(tracker.TryGetWarningKey(missingClient, LocalProtocol, 38f, 8f, out _), "reconnected roster did not warn after grace");

Console.WriteLine("inventory capability and warning tests passed");

static InventoryCapabilitySnapshot Snapshot(
    string serverVersion,
    int serverProtocol,
    params InventoryPeerCapability[] players)
{
    return new InventoryCapabilitySnapshot(
        InventoryCapabilityState.Incompatible,
        serverVersion,
        serverProtocol,
        players);
}

static InventoryPeerCapability Peer(
    string name,
    string version,
    int protocol,
    bool detected,
    bool compatible)
{
    return new InventoryPeerCapability(name, version, protocol, detected, compatible);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
