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
Expect(false, remoteRoot.TryGetProperty("peer", out _), "duplicate peer removed");
Expect(false, remoteRoot.TryGetProperty("player_id", out _), "stable player ID removed");
Expect(false, remoteRoot.TryGetProperty("creature_id", out _), "creature ID removed");
Expect(false, remoteRoot.TryGetProperty("zdo_id", out _), "network object ID removed");
Expect(false, remoteRoot.TryGetProperty("station", out _), "instance-suffixed station removed");
Expect(false, remoteRoot.TryGetProperty("position", out _), "exact position removed");
Expect(false, remoteRoot.TryGetProperty("head_position_x", out _), "exact head position removed");
Expect(false, remoteRoot.TryGetProperty("target_bounds_center_y", out _), "world bounds center removed");
Expect(false, remoteRoot.TryGetProperty("hit_point_x", out _), "world hit point removed");
Expect(false, remoteRoot.TryGetProperty("error", out _), "raw error removed");

DiagnosticEvent remoteInventory = DiagnosticEvent.Create("Inventory", "container_open_snapshot")
    .String("operation_phase", "observer_first_open_candidate")
    .String("item", "LoxMeat")
    .Boolean("owner", true)
    .Integer("revision", 7)
    .String("contents", "LoxMeat=13")
    .String("zdo_id", "durable-world-object")
    .String("future_unreviewed_field", "must-stay-local");
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
Expect(true, remoteInventoryRoot.GetProperty("owner").GetBoolean(), "Inventory owner decision preserved");
Expect(7, remoteInventoryRoot.GetProperty("revision").GetInt32(), "Inventory revision preserved");
Expect(false, remoteInventoryRoot.TryGetProperty("item", out _), "unlisted snapshot fields stay local");
Expect(false, remoteInventoryRoot.TryGetProperty("contents", out _), "chest contents stay local");
Expect(
    false,
    remoteInventoryRoot.TryGetProperty("zdo_id", out _),
    "Inventory world object ID stays local");
Expect(
    false,
    remoteInventoryRoot.TryGetProperty("future_unreviewed_field", out _),
    "new Inventory fields fail closed");

DiagnosticEvent remoteInventoryItem = DiagnosticEvent.Create("Inventory", "quick_stack_item")
    .String("operation_id", "op-inventory")
    .String("operation_phase", "write")
    .String("item", "LoxMeat")
    .Integer("moved", 13)
    .String("container", "piece_chest_wood")
    .String("location", "4m north");
remoteInventoryItem.Prepare(
    new DateTime(2026, 8, 13, 4, 5, 9, DateTimeKind.Utc),
    "session-inventory",
    "0.1.63");
using JsonDocument remoteInventoryItemJson = JsonDocument.Parse(
    remoteInventoryItem.ToRemoteJsonLine("client-random", "Johnny", "peer-session", "sha256:build"));
JsonElement remoteInventoryItemRoot = remoteInventoryItemJson.RootElement;
Expect(
    "op-inventory",
    remoteInventoryItemRoot.GetProperty("operation_id").GetString(),
    "Inventory item operation ID preserved");
Expect("LoxMeat", remoteInventoryItemRoot.GetProperty("item").GetString(), "Inventory item preserved");
Expect(13, remoteInventoryItemRoot.GetProperty("moved").GetInt32(), "Inventory moved count preserved");

DiagnosticEvent unknownInventory = DiagnosticEvent.Create("Inventory", "future_inventory_event")
    .String("operation_id", "op-future")
    .String("reason", "unreviewed");
unknownInventory.Prepare(
    new DateTime(2026, 8, 13, 4, 5, 10, DateTimeKind.Utc),
    "session-inventory",
    "0.1.63");
using JsonDocument unknownInventoryJson = JsonDocument.Parse(
    unknownInventory.ToRemoteJsonLine("client-random", "Johnny", "peer-session", "sha256:build"));
Expect(
    false,
    unknownInventoryJson.RootElement.TryGetProperty("operation_id", out _),
    "unknown Inventory event fields fail closed");

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
