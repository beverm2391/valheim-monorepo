using System;
using System.IO;
using System.Text.Json;
using BenheimQoL;
using BenheimQoL.Infrastructure;

DiagnosticEvent typed = DiagnosticEvent.Create("Production", "station_fill_finished")
    .String("operation_id", "op-1")
    .String("operation_phase", "terminal")
    .String("item", "LoxPie\nUncooked")
    .Integer("accepted", 3)
    .Number("elapsed", 1.25f)
    .Boolean("accepted_by_owner", true);
typed.Prepare(
    new DateTime(2026, 8, 13, 4, 5, 6, DateTimeKind.Utc),
    "session-1",
    "0.1.60");

using JsonDocument json = JsonDocument.Parse(typed.ToJsonLine());
JsonElement root = json.RootElement;
Expect("2026-08-13T04:05:06.0000000Z", root.GetProperty("timestamp").GetString(), "UTC timestamp");
Expect("session-1", root.GetProperty("session").GetString(), "session");
Expect("0.1.60", root.GetProperty("benheim_version").GetString(), "version");
Expect("Production", root.GetProperty("domain").GetString(), "domain");
Expect("station_fill_finished", root.GetProperty("event").GetString(), "event");
Expect(1, root.GetProperty("schema").GetInt32(), "schema");
Expect("LoxPie\nUncooked", root.GetProperty("item").GetString(), "escaped string round trip");
Expect(3, root.GetProperty("accepted").GetInt32(), "integer remains numeric");
Expect(1.25, root.GetProperty("elapsed").GetDouble(), "number remains numeric");
Expect(true, root.GetProperty("accepted_by_owner").GetBoolean(), "boolean remains boolean");
Expect(
    "[diag][Production] station_fill_finished operation_id=op-1 operation_phase=terminal item=LoxPie_Uncooked accepted=3 elapsed=1.25 accepted_by_owner=true",
    typed.ToReadableLine(),
    "readable line is rendered from the same fields");

DiagnosticEvent remoteTyped = DiagnosticEvent.Create("EnemyTiers", "boar_test_geometry")
    .String("operation_id", "op-remote")
    .String("peer", "duplicate-peer")
    .String("player_id", "stable-character")
    .String("creature_id", "creature")
    .String("zdo_id", "network-object")
    .String("station", "piece_oven#12345")
    .String("position", "100,20,300")
    .Number("head_position_x", 100f)
    .Number("target_bounds_center_y", 20f)
    .Number("hit_point_x", 101f)
    .Number("hit_point_local_x", 1f)
    .String("error", "/Users/private/path")
    .String("path", "/Users/private/diagnostic-source")
    .String("item", "LoxMeat")
    .Integer("moved", 13);
remoteTyped.Prepare(
    new DateTime(2026, 8, 13, 4, 5, 7, DateTimeKind.Utc),
    "session-remote",
    "0.1.63");
using JsonDocument remoteJson = JsonDocument.Parse(
    remoteTyped.ToRemoteJsonLine("client-random", "Johnny", "peer-session", "sha256:build"));
JsonElement remoteRoot = remoteJson.RootElement;
Expect("2026-08-13T04:05:07.0000000Z", remoteRoot.GetProperty("_time").GetString(), "Axiom time");
Expect("client-random", remoteRoot.GetProperty("client_id").GetString(), "canonical client ID");
Expect("Johnny", remoteRoot.GetProperty("player_name").GetString(), "disclosed player name");
Expect("peer-session", remoteRoot.GetProperty("peer_id").GetString(), "connection peer ID");
Expect("session-remote", remoteRoot.GetProperty("session_id").GetString(), "remote session ID");
Expect("0.1.63", remoteRoot.GetProperty("mod_version").GetString(), "remote mod version");
Expect("sha256:build", remoteRoot.GetProperty("build_id").GetString(), "remote build ID");
Expect("op-remote", remoteRoot.GetProperty("operation_id").GetString(), "operation ID preserved");
Expect("LoxMeat", remoteRoot.GetProperty("item").GetString(), "typed gameplay field preserved");
Expect(1, remoteRoot.GetProperty("hit_point_local_x").GetDouble(), "local geometry preserved");
Expect("duplicate-peer", remoteRoot.GetProperty("peer").GetString(), "typed peer preserved");
Expect("stable-character", remoteRoot.GetProperty("player_id").GetString(), "player ID preserved");
Expect("creature", remoteRoot.GetProperty("creature_id").GetString(), "creature ID preserved");
Expect("network-object", remoteRoot.GetProperty("zdo_id").GetString(), "network object ID preserved");
Expect("piece_oven#12345", remoteRoot.GetProperty("station").GetString(), "station ID preserved");
Expect("100,20,300", remoteRoot.GetProperty("position").GetString(), "position preserved");
Expect(100, remoteRoot.GetProperty("head_position_x").GetDouble(), "head position preserved");
Expect(20, remoteRoot.GetProperty("target_bounds_center_y").GetDouble(), "bounds center preserved");
Expect(101, remoteRoot.GetProperty("hit_point_x").GetDouble(), "world hit point preserved");
Expect("/Users/private/path", remoteRoot.GetProperty("error").GetString(), "typed error preserved");
Expect("/Users/private/diagnostic-source", remoteRoot.GetProperty("path").GetString(), "typed path preserved");
Expect(13, remoteRoot.GetProperty("moved").GetInt32(), "typed count preserved");

DiagnosticEvent remoteInventory = DiagnosticEvent.Create("Inventory", "container_open_snapshot")
    .Integer("peer", 42)
    .Integer("player_id", 84)
    .String("zdo_id", "durable-world-object")
    .String("station", "piece_chest_wood#123")
    .String("chest_id", "chest-123")
    .String("operation_id", "op-inventory")
    .String("transaction_id", "tx-inventory")
    .String("operation_phase", "observer_first_open_candidate")
    .String("item", "LoxMeat")
    .Integer("count", 13)
    .Boolean("owner", true)
    .Integer("revision", 7)
    .String("contents", "LoxMeat=13")
    .String("future_diagnostic_field", "shared-evidence");
remoteInventory.Prepare(
    new DateTime(2026, 8, 13, 4, 5, 8, DateTimeKind.Utc),
    "session-inventory",
    "0.1.63");
using JsonDocument localInventoryJson = JsonDocument.Parse(remoteInventory.ToJsonLine());
Expect(
    "LoxMeat=13",
    localInventoryJson.RootElement.GetProperty("contents").GetString(),
    "local chest contents remain available");
using JsonDocument remoteInventoryJson = JsonDocument.Parse(
    remoteInventory.ToRemoteJsonLine("client-random", "Johnny", "peer-session", "sha256:build"));
JsonElement remoteInventoryRoot = remoteInventoryJson.RootElement;
Expect(42, remoteInventoryRoot.GetProperty("peer").GetInt64(), "snapshot peer preserved");
Expect(84, remoteInventoryRoot.GetProperty("player_id").GetInt64(), "snapshot player ID preserved");
Expect("durable-world-object", remoteInventoryRoot.GetProperty("zdo_id").GetString(), "snapshot object ID preserved");
Expect("piece_chest_wood#123", remoteInventoryRoot.GetProperty("station").GetString(), "snapshot station ID preserved");
Expect("chest-123", remoteInventoryRoot.GetProperty("chest_id").GetString(), "snapshot chest ID preserved");
Expect("op-inventory", remoteInventoryRoot.GetProperty("operation_id").GetString(), "snapshot operation ID preserved");
Expect("tx-inventory", remoteInventoryRoot.GetProperty("transaction_id").GetString(), "snapshot transaction ID preserved");
Expect("observer_first_open_candidate", remoteInventoryRoot.GetProperty("operation_phase").GetString(), "snapshot phase preserved");
Expect("LoxMeat", remoteInventoryRoot.GetProperty("item").GetString(), "snapshot item preserved");
Expect(13, remoteInventoryRoot.GetProperty("count").GetInt32(), "snapshot count preserved");
Expect(true, remoteInventoryRoot.GetProperty("owner").GetBoolean(), "Inventory owner decision preserved");
Expect(7, remoteInventoryRoot.GetProperty("revision").GetInt32(), "Inventory revision preserved");
Expect("LoxMeat=13", remoteInventoryRoot.GetProperty("contents").GetString(), "snapshot contents preserved");
Expect("shared-evidence", remoteInventoryRoot.GetProperty("future_diagnostic_field").GetString(), "future typed fields preserved");

string testRoot = Path.Combine(Path.GetTempPath(), "benheim-events-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);
try
{
    Diagnostics.BeginSession(testRoot, "0.1.60");
    Diagnostics.Event("Core", "legacy_only", "free=form");
    Diagnostics.Emit(
        DiagnosticEvent.Create("Cooking", "owner_decision")
            .String("station", "piece_oven#1")
            .String("item", "LoxPieUncooked")
            .Boolean("accepted", false));
    Diagnostics.EndSession();

    string[] records = File.ReadAllLines(Path.Combine(testRoot, Diagnostics.CurrentEventFileName));
    Expect(1, records.Length, "only explicitly typed events enter the structured file");
    using JsonDocument written = JsonDocument.Parse(records[0]);
    Expect("Cooking", written.RootElement.GetProperty("domain").GetString(), "writer domain");
    Expect("owner_decision", written.RootElement.GetProperty("event").GetString(), "writer event");
    Expect(2, Plugin.Log.Info.Count, "legacy and typed readable lines remain visible");
    Expect(0, Plugin.Log.Warnings.Count, "healthy writer has no warning");
}
finally
{
    Directory.Delete(testRoot, recursive: true);
}

Console.WriteLine("structured diagnostic event checks passed");
return;

static void Expect<T>(T expected, T actual, string scenario)
{
    if (!Equals(expected, actual))
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}
