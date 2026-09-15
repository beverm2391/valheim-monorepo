using System;
using BenheimQoL.Interaction;

internal static class LiquidItemRecoveryTests
{
    internal static void Run()
    {
        Floating nativeWoodBuoyancy = new Floating
        {
            m_waterLevelOffset = 0.7f,
            m_forceDistance = 1.25f,
            m_force = 0.65f,
            m_balanceForceFraction = 0.03f,
            m_damping = 0.08f
        };
        UnityEngine.GameObject wood = new UnityEngine.GameObject("Wood");
        wood.AddComponent(nativeWoodBuoyancy);
        ObjectDB.instance = new ObjectDB();
        ObjectDB.instance.Add(wood);

        foreach (string prefab in new[] { "Stone", "Iron", "TrophyBoar", "SwordIron" })
        {
            ItemDrop item = PhysicalItem(
                prefab,
                new UnityEngine.Vector3(-2f, 0.5f, -1.01f));
            Expect(LiquidItemRecovery.EnsureNativeBuoyancy(item) == ItemBuoyancySetup.Added);
            Floating? floating = item.GetComponent<Floating>();
            Expect(floating != null);
            Expect(floating!.m_waterLevelOffset == nativeWoodBuoyancy.m_waterLevelOffset);
            Expect(floating.m_forceDistance == nativeWoodBuoyancy.m_forceDistance);
            Expect(floating.m_force == nativeWoodBuoyancy.m_force);
            Expect(floating.m_balanceForceFraction == nativeWoodBuoyancy.m_balanceForceFraction);
            Expect(floating.m_damping == nativeWoodBuoyancy.m_damping);
            UnityEngine.Vector3 center = item.GetComponent<UnityEngine.Rigidbody>()!.centerOfMass;
            Expect(center.x == 0f && center.y == 0.5f && center.z == 0f);
        }

        ItemDrop alreadyFloating = PhysicalItem("Wood", new UnityEngine.Vector3(0f, 0f, 0f));
        Floating existing = new Floating { m_force = 0.91f };
        alreadyFloating.gameObject.AddComponent(existing);
        Expect(LiquidItemRecovery.EnsureNativeBuoyancy(alreadyFloating) == ItemBuoyancySetup.Existing);
        Expect(ReferenceEquals(existing, alreadyFloating.GetComponent<Floating>()));
        Expect(existing.m_force == 0.91f);

        ItemDrop noBody = new ItemDrop { gameObject = new UnityEngine.GameObject("NoBody") };
        noBody.gameObject.AddComponent(new UnityEngine.Collider());
        Expect(LiquidItemRecovery.EnsureNativeBuoyancy(noBody) == ItemBuoyancySetup.MissingRigidbody);
        Expect(noBody.GetComponent<Floating>() == null);

        ItemDrop noCollider = new ItemDrop { gameObject = new UnityEngine.GameObject("NoCollider") };
        noCollider.gameObject.AddComponent(new UnityEngine.Rigidbody());
        Expect(LiquidItemRecovery.EnsureNativeBuoyancy(noCollider) == ItemBuoyancySetup.MissingCollider);
        Expect(noCollider.GetComponent<Floating>() == null);
    }

    private static ItemDrop PhysicalItem(string prefabName, UnityEngine.Vector3 centerOfMass)
    {
        ItemDrop item = new ItemDrop { gameObject = new UnityEngine.GameObject(prefabName) };
        item.gameObject.AddComponent(new UnityEngine.Rigidbody { centerOfMass = centerOfMass });
        item.gameObject.AddComponent(new UnityEngine.Collider());
        return item;
    }

    private static void Expect(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Liquid item recovery assertion failed.");
        }
    }
}
