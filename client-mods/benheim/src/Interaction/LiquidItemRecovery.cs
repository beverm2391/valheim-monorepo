using UnityEngine;

namespace BenheimQoL.Interaction;

internal enum ItemBuoyancySetup
{
    Existing,
    Added,
    MissingRigidbody,
    MissingCollider
}

/// <summary>
/// Keeps ordinary native item drops recoverable in water and tar. Buoyancy is
/// Valheim's own ephemeral component; item data, stacks, ZDOs, ownership, and
/// pickup all remain on the native paths.
/// </summary>
internal static class LiquidItemRecovery
{
    private const string NativeBuoyancyDonor = "Wood";

    internal static ItemBuoyancySetup EnsureNativeBuoyancy(ItemDrop item)
    {
        if (item.GetComponent<Floating>() != null)
        {
            return ItemBuoyancySetup.Existing;
        }

        Rigidbody? body = item.GetComponent<Rigidbody>();
        if (body == null)
        {
            return ItemBuoyancySetup.MissingRigidbody;
        }

        if (item.GetComponentInChildren<Collider>() == null)
        {
            return ItemBuoyancySetup.MissingCollider;
        }

        Floating floating = item.gameObject.AddComponent<Floating>();
        CopyNativeBuoyancyProfile(floating, FindNativeBuoyancyDonor());

        // Some item prefabs have an extreme negative center of mass. Native
        // buoyancy applies force through that point, which can launch those
        // items instead of settling them at the surface. Normalize only the
        // invalid-looking axes and preserve every ordinary rigidbody profile.
        body.centerOfMass = NormalizeExtremeCenterOfMass(body.centerOfMass);
        return ItemBuoyancySetup.Added;
    }

    internal static bool ShouldBlockPickable(Pickable _) => false;

    internal static bool ShouldBlockItemDrop(ItemDrop _) => false;

    internal static Vector3 NormalizeExtremeCenterOfMass(Vector3 center)
    {
        if (center.x < -1f)
        {
            center.x = 0f;
        }
        if (center.y < -1f)
        {
            center.y = 0f;
        }
        if (center.z < -1f)
        {
            center.z = 0f;
        }
        return center;
    }

    private static Floating? FindNativeBuoyancyDonor()
    {
        GameObject? wood = ObjectDB.instance?.GetItemPrefab(NativeBuoyancyDonor);
        return wood?.GetComponent<Floating>();
    }

    private static void CopyNativeBuoyancyProfile(Floating target, Floating? donor)
    {
        if (donor == null)
        {
            return;
        }

        target.m_waterLevelOffset = donor.m_waterLevelOffset;
        target.m_forceDistance = donor.m_forceDistance;
        target.m_force = donor.m_force;
        target.m_balanceForceFraction = donor.m_balanceForceFraction;
        target.m_damping = donor.m_damping;
    }
}
