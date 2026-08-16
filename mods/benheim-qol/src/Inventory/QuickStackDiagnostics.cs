using BenheimQoL.Infrastructure;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackDiagnostics
{
    private static readonly FieldInfo? NetworkViewField = AccessTools.Field(typeof(Container), "m_nview");
    private static readonly HashSet<string> OpenedChests = new HashSet<string>();

    internal static void ItemMoved(
        string operationId,
        ItemDrop.ItemData item,
        int moved,
        int resultingCount,
        Container container,
        string containerLocation)
    {
        ZNetView? networkView = NetworkViewField?.GetValue(container) as ZNetView;
        Diagnostics.Emit(
            DiagnosticEvent.Create("Inventory", "quick_stack_item")
                .String("operation_id", operationId)
                .String("operation_phase", "write")
                .String("item", item.m_shared.m_name)
                .Integer("moved", moved)
                .Integer("resulting_count", resultingCount)
                .String("zdo_id", StableZdoId(networkView))
                .String("container", container.gameObject.name)
                .String("location", containerLocation)
                .String(
                    "position",
                    $"{container.transform.position.x:0.##},{container.transform.position.y:0.##},{container.transform.position.z:0.##}"));
    }

    internal static void WriteSnapshot(
        string operationId,
        Container container,
        ZNetView networkView,
        int movedItems,
        uint revisionBefore,
        uint revisionAfter)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("Inventory", "quick_stack_write_snapshot")
                .String("operation_id", operationId)
                .String("operation_phase", "write_complete")
                .Integer("peer", ZDOMan.GetSessionID())
                .Integer("player_id", LocalPlayerId())
                .String("zdo_id", StableZdoId(networkView))
                .Boolean("owner", container.IsOwner())
                .Integer("revision_before", revisionBefore)
                .Integer("revision_after", revisionAfter)
                .Boolean("revision_advanced", revisionAfter > revisionBefore)
                .Integer("moved", movedItems)
                .String("contents", InventoryContents(container.GetInventory())));
    }

    internal static void ContainerOpened(Container container)
    {
        ZNetView? networkView = NetworkViewField?.GetValue(container) as ZNetView;
        if (!networkView || !networkView.IsValid())
        {
            return;
        }

        string zdoId = StableZdoId(networkView);
        if (!OpenedChests.Add(zdoId))
        {
            return;
        }

        Diagnostics.Emit(
            DiagnosticEvent.Create("Inventory", "container_open_snapshot")
                .String("operation_id", null)
                .String("operation_phase", "observer_first_open_candidate")
                .Integer("peer", ZDOMan.GetSessionID())
                .Integer("player_id", LocalPlayerId())
                .String("zdo_id", zdoId)
                .Boolean("owner", container.IsOwner())
                .Integer("revision", networkView.GetZDO().DataRevision)
                .String("contents", InventoryContents(container.GetInventory())));
    }

    private static string StableZdoId(ZNetView? networkView)
    {
        return networkView && networkView.IsValid()
            ? networkView.GetZDO().m_uid.ToString()
            : "unavailable";
    }

    private static long LocalPlayerId()
    {
        try
        {
            return Game.instance?.GetPlayerProfile()?.GetPlayerID() ?? 0L;
        }
        catch
        {
            // Evidence must fail closed if the stable character identity is
            // unavailable; the checker rejects zero and matching identities.
            return 0L;
        }
    }

    private static string InventoryContents(Inventory inventory)
    {
        SortedDictionary<string, int> totals = new SortedDictionary<string, int>();
        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
        {
            string name = item.m_shared.m_name;
            totals.TryGetValue(name, out int total);
            totals[name] = total + item.m_stack;
        }

        List<string> entries = new List<string>(totals.Count);
        foreach ((string name, int total) in totals)
        {
            entries.Add($"{name}={total}");
        }
        return string.Join(",", entries);
    }
}
