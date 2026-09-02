using System;
using System.Collections.Generic;

// Only native object/diagnostic boundaries are stubbed. Affinity identity,
// eligibility, state mutation, and projectile/draw behavior use production code.
namespace UnityEngine
{
    internal sealed class GameObject
    {
        internal GameObject(string name) { this.name = name; }
        internal string name;
    }

    internal static class Mathf
    {
        internal static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    }
}

internal sealed class ObjectDB
{
    internal static ObjectDB? instance;
    internal readonly Dictionary<string, UnityEngine.GameObject> Prefabs = new();
    internal UnityEngine.GameObject? GetItemPrefab(string name) =>
        Prefabs.TryGetValue(name, out UnityEngine.GameObject? prefab) ? prefab : null;
}

internal sealed class ItemDrop
{
    internal sealed class SharedData
    {
        internal int m_maxQuality = 4;
    }

    internal sealed class ItemData
    {
        internal UnityEngine.GameObject? m_dropPrefab;
        internal int m_quality = 4;
        internal SharedData m_shared = new();
        internal Dictionary<string, string> m_customData = new();
    }
}

internal class Character { }
internal class Humanoid : Character { }
internal sealed class Player : Humanoid
{
    internal static Player? m_localPlayer;
    internal ItemDrop.ItemData? Weapon;
    internal ItemDrop.ItemData? GetCurrentWeapon() => Weapon;
}
internal sealed class Projectile { }

namespace BenheimQoL.Infrastructure
{
    internal static class HealthReporting
    {
        internal static bool GameplayActionsEnabled = true;
    }

    internal sealed class DiagnosticEvent
    {
        internal static DiagnosticEvent Create(string domain, string name) => new();
        internal DiagnosticEvent String(string name, string value) => this;
        internal DiagnosticEvent Integer(string name, int value) => this;
        internal DiagnosticEvent Boolean(string name, bool value) => this;
    }
}

namespace BenheimQoL.Affinities
{
    internal static class AffinityDiagnostics
    {
        internal static void Emit(BenheimQoL.Infrastructure.DiagnosticEvent value) { }
    }
}
