using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BenheimQoL.Infrastructure;

Expect(
    RuntimePrimitiveCatalogRequest.TryCreate(
        RuntimePrimitiveCatalogCategory.Effects,
        Array.Empty<string>(),
        out RuntimePrimitiveCatalogRequest effects)
    && effects.Category == RuntimePrimitiveCatalogCategory.Effects
    && effects.Filter == string.Empty,
    "effects request parses without a filter");

Expect(
    RuntimePrimitiveCatalogRequest.TryCreate(
        RuntimePrimitiveCatalogCategory.Text,
        new[] { "  bronze  " },
        out RuntimePrimitiveCatalogRequest text)
    && text.Category == RuntimePrimitiveCatalogCategory.Text
    && text.Filter == "bronze",
    "request trims one filter");

Expect(
    RuntimePrimitiveCatalogRequest.TryCreate(
        RuntimePrimitiveCatalogCategory.Ui,
        Array.Empty<string>(),
        out RuntimePrimitiveCatalogRequest ui)
    && ui.Category == RuntimePrimitiveCatalogCategory.Ui,
    "ui request parses");

ExpectRejected(RuntimePrimitiveCatalogCategory.Ui, new[] { "" }, "empty filter");
ExpectRejected(RuntimePrimitiveCatalogCategory.Ui, new[] { "panel", "extra" }, "extra argument");

Expect(
    RuntimePrimitiveCatalogPolicy.IsNativeRuntimeType(typeof(string).Assembly, typeof(string).Assembly),
    "native runtime types keep matching assembly provenance");
Expect(
    !RuntimePrimitiveCatalogPolicy.IsNativeRuntimeType(
        typeof(string).Assembly,
        typeof(RuntimePrimitiveCatalogRequest).Assembly),
    "mod-owned runtime types fail native assembly provenance");
Expect(
    RuntimePrimitiveCatalogPolicy.IsPluginOwnedObjectName("BenheimShortcutsPanel"),
    "Benheim-owned UI subtrees are excluded");
Expect(
    !RuntimePrimitiveCatalogPolicy.IsPluginOwnedObjectName("InventoryGui"),
    "native UI roots remain eligible");
Expect(
    RuntimePrimitiveCatalogPolicy.StableFontIdentity("Norse", "atlas", "material")
        == RuntimePrimitiveCatalogPolicy.StableFontIdentity("Norse", "atlas", "material"),
    "equivalent fonts share one semantic identity");

RuntimePrimitiveRecord escapedRecord =
    new RuntimePrimitiveRecord("effects", "consume_status_effect", "item:\"Mead\"")
        .String("display_name", "Line one\nLine two")
        .String("missing", null)
        .Integer("name_hash", 42)
        .Boolean("icon_present", true);
using (JsonDocument json = JsonDocument.Parse(
    escapedRecord.ToJsonLine(DateTime.UnixEpoch, "test")))
{
    JsonElement root = json.RootElement;
    Expect(root.GetProperty("identity").GetString() == "item:\"Mead\"", "identity JSON escaping round-trips");
    Expect(root.GetProperty("display_name").GetString() == "Line one\nLine two", "field JSON escaping round-trips");
    Expect(root.GetProperty("missing").ValueKind == JsonValueKind.Null, "missing text stays JSON null");
    Expect(root.GetProperty("name_hash").GetInt32() == 42, "integer fields stay numeric");
    Expect(root.GetProperty("icon_present").GetBoolean(), "boolean fields stay boolean");
}
Expect(escapedRecord.Matches("line two"), "filter searches field values case-insensitively");
Expect(escapedRecord.Matches("consume_status"), "filter searches donor kinds");
Expect(!escapedRecord.Matches("bronze"), "unmatched filters reject a record");
Expect(!escapedRecord.ToConsoleLine().Contains('\n'), "console preview flattens embedded newlines");

List<RuntimePrimitiveRecord> source = new List<RuntimePrimitiveRecord>
{
    new RuntimePrimitiveRecord("ui", "panel_image", "inventory:bronze"),
    new RuntimePrimitiveRecord("ui", "button", "inventory:bronze-button"),
    new RuntimePrimitiveRecord("ui", "panel_image", "inventory:iron")
};
RuntimePrimitiveCatalogSelection selection =
    RuntimePrimitiveCatalogSelection.Create(source, "bronze", maximumEntries: 1);
Expect(selection.SourceCount == 3, "selection preserves the source count");
Expect(selection.Matches.Count == 2, "selection counts every filter match");
Expect(selection.WrittenCount == 1, "selection caps the local snapshot");
using (JsonDocument summary = JsonDocument.Parse(
    selection
        .CreateSummary("ui", "bronze", DateTime.UnixEpoch)
        .ToJsonLine(DateTime.UnixEpoch, "test")))
{
    JsonElement root = summary.RootElement;
    Expect(root.GetProperty("source_count").GetInt32() == 3, "summary reports source count");
    Expect(root.GetProperty("matched_count").GetInt32() == 2, "summary reports match count");
    Expect(root.GetProperty("written_count").GetInt32() == 1, "summary reports written count");
    Expect(root.GetProperty("truncated").GetBoolean(), "summary reports truncation");
}

RuntimePrimitiveCatalogAvailability allReady = Availability();
Expect(
    RuntimePrimitiveCatalogReadiness.TryValidate(
        RuntimePrimitiveCatalogCategory.Effects,
        allReady,
        out _),
    "effects catalog accepts a fully ready world");
Expect(
    !RuntimePrimitiveCatalogReadiness.TryValidate(
        RuntimePrimitiveCatalogCategory.Effects,
        Availability(worldReady: false),
        out string earlyFailure)
    && earlyFailure.Contains("playable world", StringComparison.Ordinal),
    "a populated ObjectDB still fails before the playable world is ready");
Expect(
    !RuntimePrimitiveCatalogReadiness.TryValidate(
        RuntimePrimitiveCatalogCategory.Effects,
        Availability(objectDatabasePopulated: false),
        out string objectDatabaseFailure)
    && objectDatabaseFailure.Contains("not populated", StringComparison.Ordinal),
    "effects catalog rejects an early ObjectDB lifecycle");
Expect(
    !RuntimePrimitiveCatalogReadiness.TryValidate(
        RuntimePrimitiveCatalogCategory.Text,
        Availability(tmpReady: false),
        out string textFailure)
    && textFailure.Contains("TMP default font", StringComparison.Ordinal),
    "text catalog rejects an early TMP lifecycle");

string testDirectory = Path.Combine(
    Path.GetTempPath(),
    "benheim-runtime-catalog-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testDirectory);
try
{
    string snapshotPath = Path.Combine(testDirectory, "catalog.ndjson");
    File.WriteAllText(snapshotPath, "old snapshot");
    try
    {
        RuntimePrimitiveSnapshotFile.WriteAtomically(snapshotPath, writer =>
        {
            writer.WriteLine("partial snapshot");
            throw new IOException("simulated write failure");
        });
        throw new InvalidOperationException("Expectation failed: simulated write failure escapes");
    }
    catch (IOException exception) when (exception.Message == "simulated write failure")
    {
    }

    Expect(File.ReadAllText(snapshotPath) == "old snapshot", "failed writes preserve the last complete snapshot");
    Expect(!File.Exists(snapshotPath + ".tmp"), "failed writes remove the temporary snapshot");

    RuntimePrimitiveSnapshotFile.WriteAtomically(
        snapshotPath,
        writer => writer.WriteLine("new snapshot"));
    Expect(File.ReadAllText(snapshotPath).Trim() == "new snapshot", "successful writes replace the snapshot");
}
finally
{
    Directory.Delete(testDirectory, recursive: true);
}

Console.WriteLine("runtime primitive catalog command tests passed");

static RuntimePrimitiveCatalogAvailability Availability(
    bool worldReady = true,
    bool objectDatabaseReady = true,
    bool objectDatabasePopulated = true,
    bool inventoryGuiReady = true,
    bool hudReady = true,
    bool messageHudReady = true,
    bool menuReady = true,
    bool menuSettingsReady = true,
    bool tmpReady = true)
{
    return new RuntimePrimitiveCatalogAvailability(
        worldReady,
        objectDatabaseReady,
        objectDatabasePopulated,
        inventoryGuiReady,
        hudReady,
        messageHudReady,
        menuReady,
        menuSettingsReady,
        tmpReady);
}

static void ExpectRejected(
    RuntimePrimitiveCatalogCategory category,
    string[] arguments,
    string description)
{
    Expect(
        !RuntimePrimitiveCatalogRequest.TryCreate(category, arguments, out _),
        $"{description} is rejected");
}

static void Expect(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Expectation failed: {description}");
    }
}
