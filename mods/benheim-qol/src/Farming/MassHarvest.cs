using System;
using System.Collections.Generic;
using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Farming;

internal static class MassHarvest
{
    private static readonly FieldInfo InteractMaskField =
        AccessTools.Field(typeof(Player), "m_interactMask")
        ?? throw new MissingFieldException(typeof(Player).FullName, "m_interactMask");

    private static readonly MethodInfo ExtractMethod =
        AccessTools.Method(typeof(Beehive), "Extract")
        ?? throw new MissingMethodException(typeof(Beehive).FullName, "Extract");

    private static readonly MethodInfo GetHoneyLevelMethod =
        AccessTools.Method(typeof(Beehive), "GetHoneyLevel")
        ?? throw new MissingMethodException(typeof(Beehive).FullName, "GetHoneyLevel");

    private static readonly HashSet<int> SeenObjects = new HashSet<int>();

    private static bool massHarvestRunning;

    internal static MassHarvestResult? Begin(Player player, GameObject target, bool hold, bool alt)
    {
        if (massHarvestRunning
            || hold
            || player.InAttack()
            || player.InDodge()
            || !FarmingInput.IsMassActionHeld())
        {
            return null;
        }

        Interactable? interactable = target.GetComponentInParent<Interactable>();
        if (interactable is not Pickable && interactable is not Beehive)
        {
            return null;
        }

        massHarvestRunning = true;
        try
        {
            if (interactable is Pickable pickable)
            {
                return HarvestPickables(player, target.transform.position, pickable, alt);
            }

            if (interactable is Beehive beehive)
            {
                return HarvestBeehives(player, target.transform.position, beehive);
            }

            return null;
        }
        finally
        {
            SeenObjects.Clear();
            massHarvestRunning = false;
        }
    }

    internal static void Complete(MassHarvestResult? result)
    {
        if (result is null)
        {
            return;
        }

        int total = result.NearbyHarvested + (result.TargetHarvestable ? 1 : 0);
        Diagnostics.Event(
            "Farming",
            "mass_harvest_finished",
            $"kind={result.Kind} item=\"{result.ItemName}\" radius={FarmingSettings.HarvestRadius:0.#} harvested={total} nearby_harvested={result.NearbyHarvested} target_harvested={Diagnostics.Bool(result.TargetHarvestable)} duplicates={result.Duplicates} ignored={result.Ignored}");
    }

    private static MassHarvestResult HarvestPickables(
        Player player,
        Vector3 center,
        Pickable target,
        bool alt)
    {
        int mask = (int)(InteractMaskField.GetValue(player) ?? 0);
        Collider[] colliders = Physics.OverlapSphere(center, FarmingSettings.HarvestRadius, mask);
        string prefabName = target.m_itemPrefab ? target.m_itemPrefab.name : string.Empty;
        int duplicates = 0;
        int mismatched = 0;
        int accepted = 0;

        SeenObjects.Add(target.GetInstanceID());
        foreach (Collider collider in colliders)
        {
            Pickable? nearby = collider ? collider.GetComponentInParent<Pickable>() : null;
            if (!nearby)
            {
                continue;
            }

            if (!SeenObjects.Add(nearby.GetInstanceID()))
            {
                duplicates++;
                continue;
            }

            string nearbyPrefabName = nearby.m_itemPrefab ? nearby.m_itemPrefab.name : string.Empty;
            if (nearbyPrefabName != prefabName)
            {
                mismatched++;
                continue;
            }

            bool harvestable = nearby.CanBePicked();
            nearby.Interact(player, false, alt);
            if (harvestable)
            {
                accepted++;
            }
        }

        return new MassHarvestResult(
            "pickable",
            prefabName,
            accepted,
            target.CanBePicked(),
            duplicates,
            mismatched);
    }

    private static MassHarvestResult HarvestBeehives(Player player, Vector3 center, Beehive target)
    {
        int mask = (int)(InteractMaskField.GetValue(player) ?? 0);
        Collider[] colliders = Physics.OverlapSphere(center, FarmingSettings.HarvestRadius, mask);
        int duplicates = 0;
        int inaccessible = 0;
        int extracted = 0;

        SeenObjects.Add(target.GetInstanceID());
        foreach (Collider collider in colliders)
        {
            Beehive? nearby = collider ? collider.GetComponentInParent<Beehive>() : null;
            if (!nearby)
            {
                continue;
            }

            if (!SeenObjects.Add(nearby.GetInstanceID()))
            {
                duplicates++;
                continue;
            }

            if (!PrivateArea.CheckAccess(nearby.transform.position))
            {
                inaccessible++;
                continue;
            }

            bool hasHoney = GetHoneyLevel(nearby) > 0;
            ExtractMethod.Invoke(nearby, null);
            if (hasHoney)
            {
                extracted++;
            }
        }

        return new MassHarvestResult(
            "beehive",
            "honey",
            extracted,
            GetHoneyLevel(target) > 0,
            duplicates,
            inaccessible);
    }

    private static int GetHoneyLevel(Beehive beehive)
    {
        return (int)(GetHoneyLevelMethod.Invoke(beehive, null) ?? 0);
    }
}

internal sealed class MassHarvestResult
{
    internal MassHarvestResult(
        string kind,
        string itemName,
        int nearbyHarvested,
        bool targetHarvestable,
        int duplicates,
        int ignored)
    {
        Kind = kind;
        ItemName = itemName;
        NearbyHarvested = nearbyHarvested;
        TargetHarvestable = targetHarvestable;
        Duplicates = duplicates;
        Ignored = ignored;
    }

    internal string Kind { get; }
    internal string ItemName { get; }
    internal int NearbyHarvested { get; }
    internal bool TargetHarvestable { get; }
    internal int Duplicates { get; }
    internal int Ignored { get; }
}
