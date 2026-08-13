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
