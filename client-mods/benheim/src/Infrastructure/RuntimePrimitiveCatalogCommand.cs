using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using TMPro;

namespace BenheimQoL.Infrastructure;

internal static class RuntimePrimitiveCatalogCommand
{
    internal const string SnapshotFileName = "BenheimRuntimeCatalog.ndjson";
    private const int MaximumSnapshotEntries = 2000;
    private const int ConsolePreviewEntries = 20;

    internal static void Run(
        RuntimePrimitiveCatalogCategory category,
        string[] arguments,
        Action<string> output)
    {
        if (!RuntimePrimitiveCatalogRequest.TryCreate(
                category,
                arguments,
                out RuntimePrimitiveCatalogRequest request))
        {
            output("Usage: bhcatalog <effects|text|ui> [filter]");
            return;
        }

        if (!TryRequireReady(request.Category, out string readinessFailure))
        {
            output($"Benheim runtime catalog unavailable: {readinessFailure}");
            return;
        }

        List<RuntimePrimitiveRecord> source = RuntimePrimitiveCatalog.Create(request.Category);
        RuntimePrimitiveCatalogSelection selection =
            RuntimePrimitiveCatalogSelection.Create(source, request.Filter, MaximumSnapshotEntries);
        string path = Path.Combine(Paths.BepInExRootPath, SnapshotFileName);
        WriteSnapshot(path, request, selection);
        PrintSummary(output, request, selection, path);
    }

    private static bool TryRequireReady(
        RuntimePrimitiveCatalogCategory category,
        out string failure)
    {
        ObjectDB? database = ObjectDB.instance;
        Menu? menu = Menu.instance;
        RuntimePrimitiveCatalogAvailability availability =
            new RuntimePrimitiveCatalogAvailability(
                worldReady: Player.m_localPlayer != null,
                objectDatabaseReady: database != null,
                objectDatabasePopulated: database != null
                    && database.m_StatusEffects.Count > 0
                    && database.m_items.Count > 0,
                inventoryGuiReady: InventoryGui.instance != null,
                hudReady: Hud.instance != null,
                messageHudReady: MessageHud.instance != null,
                menuReady: menu != null,
                menuSettingsReady: menu != null && menu.m_settingsPrefab != null,
                tmpReady: TMP_Settings.defaultFontAsset != null);
        return RuntimePrimitiveCatalogReadiness.TryValidate(category, availability, out failure);
    }

    private static void WriteSnapshot(
        string path,
        RuntimePrimitiveCatalogRequest request,
        RuntimePrimitiveCatalogSelection selection)
    {
        DateTime createdUtc = DateTime.UtcNow;
        RuntimePrimitiveSnapshotFile.WriteAtomically(path, writer =>
        {
            RuntimePrimitiveRecord summary = selection.CreateSummary(
                CategoryName(request.Category),
                request.Filter,
                createdUtc);
            writer.WriteLine(summary.ToJsonLine(createdUtc, Plugin.PluginVersion));
            for (int index = 0; index < selection.WrittenCount; index++)
            {
                writer.WriteLine(
                    selection.Matches[index].ToJsonLine(createdUtc, Plugin.PluginVersion));
            }
        });
    }

    private static void PrintSummary(
        Action<string> output,
        RuntimePrimitiveCatalogRequest request,
        RuntimePrimitiveCatalogSelection selection,
        string path)
    {
        string filterSummary = request.Filter.Length == 0
            ? "no filter"
            : $"filter={request.Filter}";
        output(
            $"Benheim runtime catalog {CategoryName(request.Category)}: " +
            $"source={selection.SourceCount} matched={selection.Matches.Count} written={selection.WrittenCount} {filterSummary}");
        output($"Local snapshot: {path}");

        int previewCount = Math.Min(selection.WrittenCount, ConsolePreviewEntries);
        for (int index = 0; index < previewCount; index++)
        {
            output($"  {selection.Matches[index].ToConsoleLine()}");
        }

        if (selection.WrittenCount > previewCount)
        {
            output($"  ... {selection.WrittenCount - previewCount} more in the local snapshot");
        }
        if (selection.Matches.Count > selection.WrittenCount)
        {
            output(
                $"  snapshot capped at {MaximumSnapshotEntries}; use a filter to inspect the remaining matches");
        }
    }

    private static string CategoryName(RuntimePrimitiveCatalogCategory category)
    {
        return category switch
        {
            RuntimePrimitiveCatalogCategory.Effects => "effects",
            RuntimePrimitiveCatalogCategory.Text => "text",
            RuntimePrimitiveCatalogCategory.Ui => "ui",
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
    }
}
