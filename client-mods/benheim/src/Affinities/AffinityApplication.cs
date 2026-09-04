using System;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Affinities;

internal sealed class AffinityApplicationResult
{
    private AffinityApplicationResult(bool applied, string reason, bool replacing)
    {
        Applied = applied;
        Reason = reason;
        Replacing = replacing;
    }

    internal bool Applied { get; }
    internal string Reason { get; }
    internal bool Replacing { get; }

    internal static AffinityApplicationResult Success(bool replacing) =>
        new(true, "applied", replacing);
    internal static AffinityApplicationResult Rejected(string reason) =>
        new(false, reason, replacing: false);
}

internal static class AffinityApplication
{
    internal static bool IsAtBaseGameForge(Player? player)
    {
        CraftingStation? station = player?.GetCurrentCraftingStation();
        return station != null
            && string.Equals(
                station.m_name,
                AffinityPresentation.ForgeNameToken,
                StringComparison.Ordinal);
    }

    internal static AffinityApplicationResult Apply(
        Player? player,
        ItemDrop.ItemData? target,
        AffinityLoadResult selected,
        bool requireForge,
        bool consumeResources,
        string source,
        bool developerBypass = false)
    {
        string validation = Validate(
            player,
            target,
            selected,
            requireForge,
            consumeResources,
            developerBypass);
        bool valid = string.Equals(validation, "valid", StringComparison.Ordinal);
        AffinityDiagnostics.Emit(
            DiagnosticEvent.Create("Affinity", "affinity_application_validation")
                .String("source", source)
                .String("affinity", selected.ToString().ToLowerInvariant())
                .Boolean("valid", valid)
                .String("reason", validation)
                .Boolean("forge_required", requireForge)
                .Boolean("resource_cost_required", consumeResources)
                .Boolean("developer_bypass", developerBypass)
                .String("item_prefab", AffinityState.ItemPrefab(target)));
        if (!valid || player == null || target == null)
        {
            return AffinityApplicationResult.Rejected(validation);
        }

        AffinityRequirementSpec requirement = AffinityPresentation.RequirementsFor(selected);
        Inventory inventory = player.GetInventory();
        bool replacing = AffinityState.Load(target, source + "_prewrite") != AffinityLoadResult.None;
        string resourceName = string.Empty;
        int resourcesBefore = 0;
        bool consumptionAttempted = false;
        int consumed = 0;
        try
        {
            if (consumeResources)
            {
                resourceName = ResourceName(requirement);
                resourcesBefore = inventory.CountItems(resourceName);
                consumptionAttempted = true;
                inventory.RemoveItem(resourceName, requirement.MaterialAmount);
                int after = inventory.CountItems(resourceName);
                consumed = AffinityRules.CountConsumed(resourcesBefore, after);
                if (consumed != requirement.MaterialAmount)
                {
                    bool restored = TryRestoreResources(inventory, requirement, consumed);
                    EmitConsumption(source, requirement, consumed, restored ? "rolled_back" : "restore_failed");
                    return AffinityApplicationResult.Rejected("resource_consumption_mismatch");
                }
                EmitConsumption(source, requirement, consumed, "consumed");
            }

            AffinityState.Write(target, selected, source, replacing);
            NotifyInventoryChanged(inventory);
            return AffinityApplicationResult.Success(replacing);
        }
        catch (Exception exception)
        {
            if (consumptionAttempted)
            {
                // Inventory.RemoveItem mutates before notifying observers. If an
                // observer throws, recover the actual delta before rolling back.
                try
                {
                    int afterFailure = inventory.CountItems(resourceName);
                    consumed = Math.Max(
                        consumed,
                        AffinityRules.CountConsumed(resourcesBefore, afterFailure));
                }
                catch
                {
                    // The previously measured delta, if any, remains the best evidence.
                }
            }
            if (consumed > 0)
            {
                bool restored = TryRestoreResources(inventory, requirement, consumed);
                EmitConsumption(
                    source,
                    requirement,
                    consumed,
                    restored ? "restored_after_failure" : "restore_failed");
            }
            try
            {
                Plugin.Log.LogWarning(
                    $"Affinity application failed without charging the player: {Diagnostics.Flatten(exception.Message)}");
            }
            catch
            {
                // The rejection result is authoritative even if logging is unavailable.
            }
            return AffinityApplicationResult.Rejected("exception");
        }
    }

    internal static string ResourceName(AffinityRequirementSpec requirement)
    {
        GameObject? prefab = ObjectDB.instance?.GetItemPrefab(requirement.MaterialPrefab);
        ItemDrop? drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        return drop?.m_itemData?.m_shared?.m_name ?? string.Empty;
    }

    internal static ItemDrop? ResourceDrop(AffinityRequirementSpec requirement)
    {
        return ObjectDB.instance?.GetItemPrefab(requirement.MaterialPrefab)?.GetComponent<ItemDrop>();
    }

    private static string Validate(
        Player? player,
        ItemDrop.ItemData? target,
        AffinityLoadResult selected,
        bool requireForge,
        bool consumeResources,
        bool developerBypass)
    {
        if (player == null) return "no_local_player";
        if (target == null) return "no_item";
        Inventory inventory = player.GetInventory();
        if (!inventory.ContainsItem(target)) return "item_moved";
        if (selected != AffinityLoadResult.Lunge
            && selected != AffinityLoadResult.Snipe
            && selected != AffinityLoadResult.Test)
        {
            return "unsupported_affinity";
        }
        if (!AffinityState.SupportsAffinity(target, selected)) return "ineligible_item";
        if (!developerBypass && !AffinityState.IsEligibleForAffinity(target, selected))
        {
            return "maximum_quality_required";
        }
        if (!developerBypass && AffinityRules.IsSameAffinity(AffinityState.Read(target), selected))
        {
            return "affinity_already_installed";
        }
        if (requireForge && !IsAtBaseGameForge(player)) return "not_at_base_game_forge";
        if (requireForge && player.GetCurrentCraftingStation()?.CheckUsable(player, false) != true)
        {
            return "forge_unusable";
        }
        if (!consumeResources) return "valid";

        AffinityRequirementSpec requirement = AffinityPresentation.RequirementsFor(selected);
        string resourceName = ResourceName(requirement);
        if (string.IsNullOrEmpty(resourceName)) return "test_resource_unavailable";
        return inventory.CountItems(resourceName) >= requirement.MaterialAmount
            ? "valid"
            : "missing_resources";
    }

    private static bool TryRestoreResources(Inventory inventory, AffinityRequirementSpec requirement, int amount)
    {
        if (amount <= 0) return true;
        try
        {
            ItemDrop.ItemData? restored = inventory.AddItem(
                requirement.MaterialPrefab,
                amount,
                1,
                0,
                0L,
                string.Empty);
            if (restored != null) return true;
        }
        catch (Exception exception)
        {
            LogError(
                $"Affinity resource restoration threw: {Diagnostics.Flatten(exception.Message)}");
        }

        LogError(
            $"Affinity resource restoration failed for {amount} {requirement.MaterialPrefab}.");
        return false;
    }

    internal static void NotifyInventoryChanged(Inventory inventory)
    {
        try
        {
            inventory.m_onChanged?.Invoke();
        }
        catch (Exception exception)
        {
            try
            {
                Plugin.Log.LogWarning(
                    $"Affinity state changed, but an inventory observer failed: {Diagnostics.Flatten(exception.Message)}");
            }
            catch
            {
                // The state mutation already succeeded; observers remain best-effort.
            }
        }
    }

    private static void LogError(string message)
    {
        try
        {
            Plugin.Log.LogError(message);
        }
        catch
        {
            // Restoration outcome is already represented by the typed event.
        }
    }

    private static void EmitConsumption(string source, AffinityRequirementSpec requirement, int amount, string outcome)
    {
        AffinityDiagnostics.Emit(
            DiagnosticEvent.Create("Affinity", "affinity_resources_consumed")
                .String("source", source)
                .String("resource_prefab", requirement.MaterialPrefab)
                .Integer("amount", amount)
                .String("outcome", outcome));
    }
}
