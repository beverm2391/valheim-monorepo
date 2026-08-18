using System;
using System.Linq;
using System.Text.Json;
using BenheimInventoryProtocol;
using BenheimQoL.Infrastructure;
using ClientSink = BenheimQoL.InventoryFeature.InventoryTransactionDiagnosticSink;
using ServerSink = BenheimServerSupport.InventoryTransactionDiagnosticSink;

const string OperationId = "0123456789abcdef0123456789abcdef";
const string Correlation = "fedcba9876543210fedcba9876543210";

InventoryTransactionDiagnosticEvent batchStart =
    InventoryTransactionDiagnosticEvent.Create("put_away_batch_started", "requester")
        .Code("operation_id", OperationId)
        .Code("operation_phase", "start")
        .Code("status", "running");
InventoryTransactionDiagnosticEvent batchTerminal =
    InventoryTransactionDiagnosticEvent.Create("put_away_batch_finished", "requester")
        .Code("operation_id", OperationId)
        .Code("operation_phase", "terminal")
        .Code("status", "completed")
        .Code("reason", "batch_finished")
        .Integer("accepted_count", 35);
Expect(
    "start",
    batchStart.Fields.Single(field => field.Name == "operation_phase").Text,
    "canonical batch start phase");
Expect(
    "terminal",
    batchTerminal.Fields.Single(field => field.Name == "operation_phase").Text,
    "canonical batch terminal phase");
Expect(
    OperationId,
    batchTerminal.Fields.Single(field => field.Name == "operation_id").Text,
    "batch terminal preserves its start operation ID");

ClientSink.Instance.Emit(
    InventoryTransactionDiagnosticEvent.Create("client_reservation_sent", "requester")
        .Code("operation_id", OperationId)
        .Code("correlation", Correlation)
        .Code("chest_id", "123:456")
        .Code("operation_phase", "start")
        .Code("status", "sent")
        .Integer("attempt", 1)
        .Integer("owner_peer", -42)
        .Integer("revision_before", 12)
        .Integer("requested_count", 35)
        .Text("requested_items", "$item_stone=35")
        .Text("contents_before", "$item_resin=30,$item_stone=2"));

DiagnosticEvent clientEvent = Diagnostics.Captured.Single();
clientEvent.Prepare(
    new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc),
    "session-client",
    "0.1.63");
using JsonDocument localJson = JsonDocument.Parse(clientEvent.ToJsonLine());
JsonElement local = localJson.RootElement;
Expect("InventoryTransaction", local.GetProperty("domain").GetString(), "typed domain");
Expect("client_reservation_sent", local.GetProperty("event").GetString(), "typed event");
Expect(OperationId, local.GetProperty("operation_id").GetString(), "batch operation correlation");
Expect(Correlation, local.GetProperty("correlation").GetString(), "cross-peer correlation");
Expect(1, local.GetProperty("attempt").GetInt32(), "attempt remains an integer");
Expect(35, local.GetProperty("requested_count").GetInt32(), "requested count remains an integer");
Expect("123:456", local.GetProperty("chest_id").GetString(), "stable chest identity remains present");
Expect(-42L, local.GetProperty("owner_peer").GetInt64(), "signed owner peer remains typed");
Expect(12, local.GetProperty("revision_before").GetInt32(), "chest revision remains an integer");
Expect("$item_stone=35", local.GetProperty("requested_items").GetString(), "requested item counts remain present");
Expect(
    "$item_resin=30,$item_stone=2",
    local.GetProperty("contents_before").GetString(),
    "chest contents remain present for convergence diagnosis");

InventoryTransactionDiagnosticEvent ownerEvent =
    InventoryTransactionDiagnosticEvent.Create("owner_result", "chest_owner")
        .Code("correlation", Correlation)
        .Code("operation_phase", "owner_apply")
        .Code("status", "success")
        .Code("reason", "accepted")
        .Integer("requested_count", 35)
        .Integer("accepted_count", 30);
Expect(
    true,
    ownerEvent.Fields.Single(field => field.Name == "correlation").Text == Correlation,
    "requester and owner events share one transaction correlation");

InventoryTransactionDiagnosticEvent receiptCapacity =
    InventoryTransactionDiagnosticEvent.Create(
            "owner_receipt_capacity",
            "chest_owner",
            InventoryTransactionDiagnosticLevel.Warning)
        .Code("correlation", Correlation)
        .Code("operation_phase", "owner_apply")
        .Code("status", "rejected")
        .Code("reason", "receipt_capacity")
        .Integer("requested_count", 35);
BepInEx.Logging.ManualLogSource serverLog = new BepInEx.Logging.ManualLogSource();
new ServerSink(serverLog).Emit(receiptCapacity);
Expect(1, serverLog.Warnings.Count, "server warning severity");
Expect(
    true,
    serverLog.Warnings[0].Contains("correlation=" + Correlation, StringComparison.Ordinal),
    "server readable line preserves cross-peer correlation");

ClientSink.Instance.Emit(
    InventoryTransactionDiagnosticEvent.Create(
            "client_refund_dropped",
            "requester",
            InventoryTransactionDiagnosticLevel.Warning)
        .Code("operation_id", OperationId)
        .Code("correlation", Correlation)
        .Code("operation_phase", "refund")
        .Code("status", "dropped")
        .Code("reason", "inventory_full")
        .Integer("dropped_count", 5));
Expect(
    true,
    BenheimQoL.InventoryFeature.TopLeftFeedbackHud.Messages.Contains(
        "Put Away refund dropped nearby. Pick it up."),
    "world-drop fallback has prominent visible feedback");
ClientSink.Instance.Emit(
    InventoryTransactionDiagnosticEvent.Create("future_evidence", "observer")
        .Code("error_path", "/Users/example/Valheim/log.txt")
        .Text("future_payload", "arbitrary typed evidence with spaces")
        .Number("position_x", -410.64)
        .Integer("future_signed_value", -99));
DiagnosticEvent futureEvent = Diagnostics.Captured.Last();
futureEvent.Prepare(
    new DateTime(2026, 8, 16, 12, 0, 1, DateTimeKind.Utc),
    "session-client",
    "0.1.63");
using JsonDocument futureJson = JsonDocument.Parse(futureEvent.ToJsonLine());
Expect(
    "/Users/example/Valheim/log.txt",
    futureJson.RootElement.GetProperty("error_path").GetString(),
    "path-shaped typed evidence is preserved");
Expect(
    "arbitrary typed evidence with spaces",
    futureJson.RootElement.GetProperty("future_payload").GetString(),
    "future string fields are preserved without a name allowlist");
Expect(-410.64, futureJson.RootElement.GetProperty("position_x").GetDouble(),
    "negative position evidence remains typed");
Expect(-99L, futureJson.RootElement.GetProperty("future_signed_value").GetInt64(),
    "future signed integer evidence remains typed");

Console.WriteLine("inventory transaction typed diagnostic schema checks passed");

static void Expect<T>(T expected, T actual, string scenario)
{
    if (!Equals(expected, actual))
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}
