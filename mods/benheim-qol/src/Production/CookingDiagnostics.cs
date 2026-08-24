using System;
using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.Production;

// CookingStation's native add RPC carries only the item prefab. These events
// therefore report the requester removal and owner decision as separate
// observations instead of inventing correlation that the protocol cannot prove.
internal static class CookingDiagnostics
{
    private const string OvenPrefab = "piece_oven";
    private const int DoneStatus = 1;
    private const int BurntStatus = 2;
    private static readonly System.Reflection.FieldInfo NetViewField =
        AccessTools.Field(typeof(CookingStation), "m_nview");

    internal static bool IsOven(CookingStation station)
    {
        return Utils.GetPrefabName(station.gameObject) == OvenPrefab;
    }

    internal static RequestObservation BeginRequest(
        CookingStation station,
        Humanoid user,
        ItemDrop.ItemData item)
    {
        if (!IsOven(station))
        {
            return default;
        }

        Inventory inventory = user.GetInventory();
        return new RequestObservation(
            true,
            ItemPrefab(item),
            item.m_shared.m_name,
            inventory.CountItems(item.m_shared.m_name),
            FreeSlots(station),
            PeerId(),
            OwnerPeer(station));
    }

    internal static void FinishRequest(
        CookingStation station,
        Humanoid user,
        bool nativeResult,
        RequestObservation observation)
    {
        if (!observation.Tracked)
        {
            return;
        }

        int inventoryAfter = user.GetInventory().CountItems(observation.SharedItemName);
        int removed = Math.Max(0, observation.InventoryBefore - inventoryAfter);
        Diagnostics.Emit(
            DiagnosticEvent.Create("Cooking", "requester_attempt")
                .String("station", Identity(station))
                .String("item", observation.Item)
                .Integer("requester_peer", observation.RequesterPeer)
                .Integer("observed_owner_peer", observation.ObservedOwnerPeer)
                .Integer("free_slots_observed", observation.FreeSlotsBefore)
                .Integer("inventory_before", observation.InventoryBefore)
                .Integer("inventory_after", inventoryAfter)
                .Integer("removed", removed)
                .Boolean("native_result", nativeResult));
    }

    internal static OwnerObservation BeginOwnerDecision(
        CookingStation station,
        long requester,
        string item)
    {
        if (!IsOven(station))
        {
            return default;
        }

        return new OwnerObservation(
            true,
            item,
            requester,
            PeerId(),
            FreeSlots(station),
            IsAllowed(station, item));
    }

    internal static void FinishOwnerDecision(
        CookingStation station,
        OwnerObservation observation)
    {
        if (!observation.Tracked)
        {
            return;
        }

        int freeAfter = FreeSlots(station);
        bool accepted = freeAfter < observation.FreeSlotsBefore;
        string result = accepted
            ? "accepted"
            : !observation.Allowed
                ? "rejected_item"
                : observation.FreeSlotsBefore == 0
                    ? "rejected_full"
                    : "rejected_no_slot_change";
        Diagnostics.Emit(
            DiagnosticEvent.Create("Cooking", "owner_decision")
                .String("station", Identity(station))
                .String("item", observation.Item)
                .Integer("requester_peer", observation.RequesterPeer)
                .Integer("owner_peer", observation.OwnerPeer)
                .Boolean("allowed", observation.Allowed)
                .Integer("free_slots_before", observation.FreeSlotsBefore)
                .Integer("free_slots_after", freeAfter)
                .Boolean("accepted", accepted)
                .String("result", result));
    }

    internal static SlotObservation[] Snapshot(CookingStation station)
    {
        if (!IsOven(station) || !IsOwner(station))
        {
            return Array.Empty<SlotObservation>();
        }

        SlotObservation[] observations = new SlotObservation[station.m_slots.Length];
        ZDO? zdo = View(station)?.GetZDO();
        if (zdo == null)
        {
            return Array.Empty<SlotObservation>();
        }
        for (int slot = 0; slot < observations.Length; slot++)
        {
            observations[slot] = new SlotObservation(
                zdo.GetString("slot" + slot),
                zdo.GetInt("slotstatus" + slot));
        }
        return observations;
    }

    internal static void ReportTransitions(
        CookingStation station,
        SlotObservation[] before)
    {
        if (before.Length == 0)
        {
            return;
        }

        SlotObservation[] after = Snapshot(station);
        if (after.Length != before.Length)
        {
            return;
        }
        for (int slot = 0; slot < after.Length; slot++)
        {
            if (before[slot].Item == after[slot].Item && before[slot].Status == after[slot].Status)
            {
                continue;
            }

            if (after[slot].Status == DoneStatus)
            {
                Diagnostics.Emit(
                    DiagnosticEvent.Create("Cooking", "output_cooked")
                        .String("station", Identity(station))
                        .Integer("owner_peer", PeerId())
                        .Integer("slot", slot)
                        .String("input_item", before[slot].Item)
                        .String("item", after[slot].Item));
            }
            else if (after[slot].Status == BurntStatus)
            {
                Diagnostics.Emit(
                    DiagnosticEvent.Create("Cooking", "output_burned")
                        .String("station", Identity(station))
                        .Integer("owner_peer", PeerId())
                        .Integer("slot", slot)
                        .String("previous_item", before[slot].Item)
                        .String("item", after[slot].Item));
            }
        }
    }

    internal static void OutputSpawned(CookingStation station, string item, int slot)
    {
        if (!IsOven(station))
        {
            return;
        }

        Diagnostics.Emit(
            DiagnosticEvent.Create("Cooking", "output_spawned")
                .String("station", Identity(station))
                .String("item", item)
                .Integer("owner_peer", PeerId())
                .Integer("slot", slot));
    }

    internal static string Identity(CookingStation station)
    {
        return $"{Diagnostics.Flatten(Utils.GetPrefabName(station.gameObject))}#{station.GetInstanceID()}";
    }

    private static bool IsAllowed(CookingStation station, string item)
    {
        foreach (CookingStation.ItemConversion conversion in station.m_conversion)
        {
            if (conversion.m_from.gameObject.name == item)
            {
                return true;
            }
        }
        return false;
    }

    private static int FreeSlots(CookingStation station)
    {
        ZDO? zdo = View(station)?.GetZDO();
        if (zdo == null)
        {
            return -1;
        }

        int free = 0;
        for (int slot = 0; slot < station.m_slots.Length; slot++)
        {
            if (zdo.GetString("slot" + slot) == string.Empty)
            {
                free++;
            }
        }
        return free;
    }

    private static bool IsOwner(CookingStation station)
    {
        return View(station)?.IsOwner() == true;
    }

    private static ZNetView? View(CookingStation station)
    {
        return NetViewField.GetValue(station) as ZNetView;
    }

    private static long OwnerPeer(CookingStation station)
    {
        return View(station)?.GetZDO()?.GetOwner() ?? 0;
    }

    private static long PeerId()
    {
        return ZDOMan.instance == null ? 0 : ZDOMan.GetSessionID();
    }

    private static string ItemPrefab(ItemDrop.ItemData item)
    {
        return item.m_dropPrefab == null ? "unknown" : item.m_dropPrefab.name;
    }

    internal readonly struct RequestObservation
    {
        internal RequestObservation(
            bool tracked,
            string item,
            string sharedItemName,
            int inventoryBefore,
            int freeSlotsBefore,
            long requesterPeer,
            long observedOwnerPeer)
        {
            Tracked = tracked;
            Item = item;
            SharedItemName = sharedItemName;
            InventoryBefore = inventoryBefore;
            FreeSlotsBefore = freeSlotsBefore;
            RequesterPeer = requesterPeer;
            ObservedOwnerPeer = observedOwnerPeer;
        }

        internal bool Tracked { get; }
        internal string Item { get; }
        internal string SharedItemName { get; }
        internal int InventoryBefore { get; }
        internal int FreeSlotsBefore { get; }
        internal long RequesterPeer { get; }
        internal long ObservedOwnerPeer { get; }
    }

    internal readonly struct OwnerObservation
    {
        internal OwnerObservation(
            bool tracked,
            string item,
            long requesterPeer,
            long ownerPeer,
            int freeSlotsBefore,
            bool allowed)
        {
            Tracked = tracked;
            Item = item;
            RequesterPeer = requesterPeer;
            OwnerPeer = ownerPeer;
            FreeSlotsBefore = freeSlotsBefore;
            Allowed = allowed;
        }

        internal bool Tracked { get; }
        internal string Item { get; }
        internal long RequesterPeer { get; }
        internal long OwnerPeer { get; }
        internal int FreeSlotsBefore { get; }
        internal bool Allowed { get; }
    }

    internal readonly struct SlotObservation
    {
        internal SlotObservation(string item, int status)
        {
            Item = item;
            Status = status;
        }

        internal string Item { get; }
        internal int Status { get; }
    }
}
