using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL;

internal static class NearbyContainerIndex
{
    private const int MaxColliders = 96;

    private static readonly Collider[] ColliderBuffer = new Collider[MaxColliders];
    private static readonly List<Container> Containers = new List<Container>(MaxColliders);
    private static readonly HashSet<Container> SeenContainers = new HashSet<Container>();

    private static readonly MethodInfo CheckAccessMethod =
        AccessTools.Method(typeof(Container), "CheckAccess");

    private static readonly FieldInfo NetViewField =
        AccessTools.Field(typeof(Container), "m_nview");

    internal static List<Container> FindAccessibleContainers(Player player, float radius, Container? currentContainer)
    {
        Containers.Clear();
        SeenContainers.Clear();

        if (currentContainer && CanUseContainer(currentContainer, currentContainer))
        {
            AddContainer(currentContainer);
        }

        int count = Physics.OverlapSphereNonAlloc(
            player.transform.position,
            radius,
            ColliderBuffer,
            LayerMask.GetMask("piece", "piece_nonsolid", "Default", "Default_small", "vehicle"));

        for (int i = 0; i < count; i++)
        {
            Collider collider = ColliderBuffer[i];
            if (!collider)
            {
                continue;
            }

            Container container = collider.GetComponentInParent<Container>();
            if (!container || !CanUseContainer(container, currentContainer))
            {
                continue;
            }

            AddContainer(container);
        }

        Containers.Sort((left, right) =>
            Vector3.SqrMagnitude(left.transform.position - player.transform.position)
                .CompareTo(Vector3.SqrMagnitude(right.transform.position - player.transform.position)));
        return Containers;
    }

    internal static bool TryClaim(Container container)
    {
        ZNetView? netView = (ZNetView?)NetViewField.GetValue(container);
        if (!netView || !netView.IsValid())
        {
            return false;
        }

        if (!netView.IsOwner())
        {
            netView.ClaimOwnership();
        }

        return netView.IsOwner();
    }

    private static void AddContainer(Container container)
    {
        if (SeenContainers.Add(container))
        {
            Containers.Add(container);
        }
    }

    private static bool CanUseContainer(Container container, Container? currentContainer)
    {
        if (container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position, 0f, flash: false))
        {
            return false;
        }

        if (container != currentContainer && container.IsInUse())
        {
            return false;
        }

        try
        {
            long playerID = Game.instance.GetPlayerProfile().GetPlayerID();
            return (bool)(CheckAccessMethod.Invoke(container, new object[] { playerID }) ?? false);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Could not check container access: {ex.Message}");
            return false;
        }
    }
}
