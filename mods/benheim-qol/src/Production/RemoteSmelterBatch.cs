using System;
using System.Collections.Generic;
using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Production;

// Valheim's native remote Smelter RPC adds one item at a time. This protocol
// reserves one compatible material type, then lets the current station owner
// clamp the count against live state and apply it without replication waits.
internal static class RemoteSmelterBatch
{
    private const string RequestRpc = "Benheim_StationFill_Request";
    private const string ResultRpc = "Benheim_StationFill_Result";
    internal const int OreInput = 0;
    internal const int FuelInput = 1;

    private static readonly MethodInfo GetQueue = AccessTools.Method(typeof(Smelter), "GetQueueSize");
    private static readonly MethodInfo GetFuel = AccessTools.Method(typeof(Smelter), "GetFuel");
    private static readonly MethodInfo QueueOre = AccessTools.Method(typeof(Smelter), "QueueOre");
    private static readonly MethodInfo SetFuel = AccessTools.Method(typeof(Smelter), "SetFuel");
    private static readonly MethodInfo SetAnimation = AccessTools.Method(typeof(Smelter), "SetAnimation");
    private static readonly FieldInfo AddedOreTime = AccessTools.Field(typeof(Smelter), "m_addedOreTime");
    private static readonly FieldInfo NetViewField = AccessTools.Field(typeof(Smelter), "m_nview");

    // A routed RPC has no synchronous return value. Keep only the reservation
    // needed to refund this station/input when its ordinary result arrives.
    private static readonly Dictionary<string, PendingFill> Pending =
        new Dictionary<string, PendingFill>();

    internal static void Register(Smelter station)
    {
        ZNetView? view = View(station);
        if (view?.IsValid() != true)
        {
            return;
        }

        view.Register<int, ZPackage>(RequestRpc, (sender, input, package) =>
            HandleRequest(station, sender, input, package));
        view.Register<int, int>(ResultRpc, (sender, input, accepted) =>
            HandleResult(station, sender, input, accepted));
    }

    internal static bool ShouldUse(Smelter station, Humanoid user, bool invokingVanilla)
    {
        ZNetView? view = View(station);
        return !invokingVanilla && InputState.IsShiftHeld() && user == Player.m_localPlayer &&
            view?.IsValid() == true && view.GetZDO()?.HasOwner() == true &&
            StationFillBatchRules.UsesOwnerBatch(view.IsOwner());
    }

    internal static ItemDrop.ItemData? SelectFirstMaterial(
        Smelter station,
        Inventory inventory,
        ItemDrop.ItemData? selected,
        int input)
    {
        if (selected != null)
        {
            return IsAllowed(station, input, selected.m_dropPrefab.name) ? selected : null;
        }

        if (input == FuelInput)
        {
            return station.m_fuelItem == null
                ? null
                : inventory.GetItem(station.m_fuelItem.m_itemData.m_shared.m_name);
        }

        List<int> counts = new List<int>(station.m_conversion.Count);
        foreach (Smelter.ItemConversion conversion in station.m_conversion)
        {
            counts.Add(inventory.CountItems(conversion.m_from.m_itemData.m_shared.m_name));
        }
        int selectedIndex = StationFillBatchRules.FirstAvailableIndex(counts);
        return selectedIndex < 0
            ? null
            : inventory.GetItem(station.m_conversion[selectedIndex].m_from.m_itemData.m_shared.m_name);
    }

    internal static bool TryStart(
        Smelter station,
        Humanoid user,
        ItemDrop.ItemData? selected,
        int input)
    {
        Inventory inventory = user.GetInventory();
        ItemDrop.ItemData? material = SelectFirstMaterial(station, inventory, selected, input);
        if (material == null)
        {
            return false;
        }

        string key = Key(station, input);
        if (Pending.ContainsKey(key))
        {
            return true;
        }

        int capacity = input == OreInput ? station.m_maxOre : station.m_maxFuel;
        int requested = StationFillBatchRules.RequestedCount(
            inventory.CountItems(material.m_shared.m_name),
            capacity);
        if (requested == 0)
        {
            return false;
        }

        ItemDrop.ItemData refundTemplate = material.Clone();
        refundTemplate.m_stack = 1;
        inventory.RemoveItem(material.m_shared.m_name, requested);

        ZNetView view = View(station)!;
        long owner = view.GetZDO().GetOwner();
        string operationId = Diagnostics.NewOperationId();
        PendingFill pending = new PendingFill(
            operationId,
            input,
            owner,
            requested,
            refundTemplate,
            user,
            inventory,
            Time.unscaledTime);
        Pending.Add(key, pending);

        string prefab = material.m_dropPrefab.name;
        Diagnostics.Emit(
            DiagnosticEvent.Create("Production", "station_fill_requested")
                .String("operation_id", operationId)
                .String("operation_phase", "start")
                .String("station", Identity(station))
                .String("input", InputName(input))
                .String("item", prefab)
                .Integer("requester_peer", ZDOMan.GetSessionID())
                .Integer("owner_peer", owner)
                .Integer("requested", requested));

        ZPackage package = new ZPackage();
        package.Write(requested);
        package.Write(prefab);
        // Append correlation after the established payload. Older owners read
        // the same requested/item fields and ignore the tail.
        package.Write(operationId);
        try
        {
            view.InvokeRPC(RequestRpc, input, package);
        }
        catch (Exception ex)
        {
            Pending.Remove(key);
            Refund(pending, 0, out int returned, out int dropped);
            Finish(station, pending, 0, returned, dropped, "request_failed", ex.Message);
        }
        return true;
    }

    private static void HandleRequest(Smelter station, long requester, int input, ZPackage package)
    {
        ZNetView? view = View(station);
        if (view?.IsOwner() != true)
        {
            return;
        }

        string operationId = string.Empty;
        int requested = 0;
        string prefab = string.Empty;
        bool valid = false;
        try
        {
            requested = package.ReadInt();
            prefab = package.ReadString();
            operationId = package.GetPos() < package.Size()
                ? package.ReadString()
                : string.Empty;
            int limit = input == OreInput ? station.m_maxOre : station.m_maxFuel;
            valid = (input == OreInput || input == FuelInput) &&
                requested > 0 && requested <= limit && IsAllowed(station, input, prefab);
        }
        catch (Exception ex)
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("Production", "station_fill_request_rejected")
                    .String("operation_id", operationId)
                    .String("operation_phase", "decision")
                    .String("station", Identity(station))
                    .String("input", InputName(input))
                    .String("item", prefab)
                    .Integer("requester_peer", requester)
                    .Integer("owner_peer", ZDOMan.GetSessionID())
                    .Integer("requested", requested)
                    .Integer("accepted", 0)
                    .String("result", "malformed")
                    .String("error", ex.Message));
        }

        float before = Level(station, input);
        float capacity = input == OreInput ? station.m_maxOre : station.m_maxFuel;
        int accepted = StationFillBatchRules.AcceptedCount(before, capacity, requested, valid);
        if (accepted > 0)
        {
            Apply(station, input, prefab, accepted, before);
        }

        string result = accepted == 0 ? "rejected" : accepted < requested ? "partial" : "complete";
        Diagnostics.Emit(
            DiagnosticEvent.Create("Production", "station_fill_owner_result")
                .String("operation_id", operationId)
                .String("operation_phase", "decision")
                .String("station", Identity(station))
                .String("input", InputName(input))
                .String("item", prefab)
                .Integer("requester_peer", requester)
                .Integer("owner_peer", ZDOMan.GetSessionID())
                .Integer("requested", requested)
                .Integer("accepted", accepted)
                .Number("level_before", before)
                .Number("level_after", Level(station, input))
                .String("result", result));
        view.InvokeRPC(requester, ResultRpc, input, accepted);
    }

    private static void HandleResult(Smelter station, long owner, int input, int accepted)
    {
        string key = Key(station, input);
        if (!Pending.TryGetValue(key, out PendingFill? pending))
        {
            return;
        }

        if (owner != pending.Owner || input != pending.Input)
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("Production", "station_fill_result_ignored")
                    .String("operation_id", pending.OperationId)
                    .String("station", Identity(station))
                    .String("input", InputName(input))
                    .Integer("requester_peer", ZDOMan.GetSessionID())
                    .Integer("owner_peer", owner)
                    .Integer("expected_owner_peer", pending.Owner));
            return;
        }

        int bounded = Math.Max(0, Math.Min(accepted, pending.Requested));
        Refund(pending, bounded, out int returned, out int dropped);
        Pending.Remove(key);
        if (bounded > 0 && input == OreInput)
        {
            MarkOreAdded(station);
        }
        Finish(
            station,
            pending,
            bounded,
            returned,
            dropped,
            bounded == 0 ? "rejected" : bounded < pending.Requested ? "partial" : "complete");
    }

    private static bool IsAllowed(Smelter station, int input, string prefab)
    {
        if (input == FuelInput)
        {
            return station.m_fuelItem != null && station.m_fuelItem.gameObject.name == prefab;
        }
        foreach (Smelter.ItemConversion conversion in station.m_conversion)
        {
            if (conversion.m_from.gameObject.name == prefab)
            {
                return true;
            }
        }
        return false;
    }

    private static float Level(Smelter station, int input)
    {
        return Convert.ToSingle((input == OreInput ? GetQueue : GetFuel).Invoke(station, null));
    }

    private static void Apply(Smelter station, int input, string prefab, int accepted, float before)
    {
        if (input == OreInput)
        {
            for (int index = 0; index < accepted; index++)
            {
                QueueOre.Invoke(station, new object[] { prefab });
                station.m_oreAddedEffects.Create(station.transform.position, station.transform.rotation);
            }
            return;
        }

        SetFuel.Invoke(station, new object[] { before + accepted });
        for (int index = 0; index < accepted; index++)
        {
            station.m_fuelAddedEffects.Create(
                station.transform.position,
                station.transform.rotation,
                station.transform);
        }
    }

    private static void Refund(PendingFill pending, int accepted, out int returned, out int dropped)
    {
        returned = 0;
        dropped = 0;
        for (int index = accepted; index < pending.Requested; index++)
        {
            ItemDrop.ItemData item = pending.RefundTemplate.Clone();
            item.m_stack = 1;
            if (pending.Inventory.AddItem(item))
            {
                returned++;
                continue;
            }

            ItemDrop drop = ItemDrop.DropItem(
                item,
                1,
                pending.User.transform.position + pending.User.transform.forward + pending.User.transform.up,
                pending.User.transform.rotation);
            if (pending.User.IsPlayer())
            {
                drop.OnPlayerDrop();
            }
            dropped++;
        }
    }

    private static void Finish(
        Smelter station,
        PendingFill pending,
        int accepted,
        int returned,
        int dropped,
        string result,
        string? error = null)
    {
        if (pending.User)
        {
            pending.User.Message(
                MessageHud.MessageType.Center,
                accepted == 1 ? "Filled 1 item" : $"Filled {accepted} items");
        }
        DiagnosticEvent diagnosticEvent =
            DiagnosticEvent.Create("Production", "station_fill_finished")
                .String("operation_id", pending.OperationId)
                .String("operation_phase", "terminal")
                .String("station", Identity(station))
                .String("input", InputName(pending.Input))
                .String("item", pending.ItemPrefab)
                .Integer("requester_peer", ZDOMan.GetSessionID())
                .Integer("owner_peer", pending.Owner)
                .Integer("requested", pending.Requested)
                .Integer("accepted", accepted)
                .Integer("refunded", returned)
                .Integer("dropped", dropped)
                .String("result", result)
                .Number("elapsed", Time.unscaledTime - pending.StartedAt);
        if (error != null)
        {
            diagnosticEvent.String("error", error);
        }
        Diagnostics.Emit(diagnosticEvent);
    }

    private static void MarkOreAdded(Smelter station)
    {
        try
        {
            AddedOreTime.SetValue(station, Time.time);
            if (station.m_addOreAnimationDuration > 0f)
            {
                SetAnimation.Invoke(station, new object[] { true });
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Station fill animation failed: {ex.Message}");
        }
    }

    private static ZNetView? View(Smelter station) => NetViewField.GetValue(station) as ZNetView;
    private static string Key(Smelter station, int input) => $"{station.GetInstanceID()}:{input}";
    private static string InputName(int input) => input == FuelInput ? "smelter_fuel" : "smelter_input";
    private static string Identity(Smelter station) =>
        $"{Diagnostics.Flatten(station.gameObject.name)}#{station.GetInstanceID()}";

    private sealed class PendingFill
    {
        internal PendingFill(
            string operationId,
            int input,
            long owner,
            int requested,
            ItemDrop.ItemData refundTemplate,
            Humanoid user,
            Inventory inventory,
            float startedAt)
        {
            OperationId = operationId;
            Input = input;
            Owner = owner;
            Requested = requested;
            RefundTemplate = refundTemplate;
            User = user;
            Inventory = inventory;
            StartedAt = startedAt;
            ItemPrefab = refundTemplate.m_dropPrefab == null
                ? "unknown"
                : refundTemplate.m_dropPrefab.name;
        }

        internal string OperationId { get; }
        internal int Input { get; }
        internal long Owner { get; }
        internal int Requested { get; }
        internal ItemDrop.ItemData RefundTemplate { get; }
        internal Humanoid User { get; }
        internal Inventory Inventory { get; }
        internal float StartedAt { get; }
        internal string ItemPrefab { get; }
    }
}
