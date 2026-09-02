using System;
using System.Collections.Generic;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute
    {
        internal HarmonyPatch(Type type, string methodName) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPostfix : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPrefix : Attribute { }
}

namespace UnityEngine
{
    internal class Object
    {
        public static implicit operator bool(Object? value) => value is not null;
    }

    internal class Component : Object
    {
        internal GameObject gameObject = null!;
        internal Transform transform => gameObject.transform;
        internal T? GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }

    internal sealed class GameObject : Object
    {
        private readonly Dictionary<Type, Component> components = new();
        private readonly List<GameObject> children = new();

        internal GameObject(string name)
        {
            this.name = name;
            transform = AddComponent<Transform>();
        }

        internal string name { get; }
        internal bool activeSelf { get; private set; } = true;
        internal bool activeInHierarchy => activeSelf && (transform.parent?.gameObject.activeInHierarchy ?? true);
        internal Transform transform { get; }

        internal void SetActive(bool active) => activeSelf = active;

        internal void AddChild(GameObject child)
        {
            children.Add(child);
            child.transform.parent = transform;
        }

        internal T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObject = this };
            components[typeof(T)] = component;
            return component;
        }

        internal T? GetComponent<T>() where T : Component
        {
            foreach (Component component in components.Values)
            {
                if (component is T match) return match;
            }

            return null;
        }

        internal T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component
        {
            var result = new List<T>();
            AddComponents(this, includeInactive, result);
            return result.ToArray();
        }

        private static void AddComponents<T>(GameObject current, bool includeInactive, List<T> result)
            where T : Component
        {
            if (includeInactive || current.activeInHierarchy)
            {
                T? component = current.GetComponent<T>();
                if (component is not null) result.Add(component);
                foreach (GameObject child in current.children) AddComponents(child, includeInactive, result);
            }
        }
    }

    internal sealed class Transform : Component
    {
        internal Transform? parent;
        internal Vector3 localPosition = Vector3.zero;
        internal Vector3 localScale = Vector3.one;

        internal Vector3 TransformPoint(Vector3 point)
        {
            Vector3 transformed = Vector3.Scale(point, localScale) + localPosition;
            return parent is null ? transformed : parent.TransformPoint(transformed);
        }

        internal Vector3 InverseTransformPoint(Vector3 point)
        {
            Vector3 parentPoint = parent is null ? point : parent.InverseTransformPoint(point);
            return new Vector3(
                (parentPoint.x - localPosition.x) / localScale.x,
                (parentPoint.y - localPosition.y) / localScale.y,
                (parentPoint.z - localPosition.z) / localScale.z);
        }
    }

    internal struct Vector3
    {
        internal Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        internal float x;
        internal float y;
        internal float z;
        internal static Vector3 zero => new(0f, 0f, 0f);
        internal static Vector3 one => new(1f, 1f, 1f);

        internal float this[int index]
        {
            get => index switch { 0 => x, 1 => y, _ => z };
            set
            {
                if (index == 0) x = value;
                else if (index == 1) y = value;
                else z = value;
            }
        }

        internal static Vector3 Scale(Vector3 left, Vector3 right) =>
            new(left.x * right.x, left.y * right.y, left.z * right.z);

        public static Vector3 operator +(Vector3 left, Vector3 right) =>
            new(left.x + right.x, left.y + right.y, left.z + right.z);

        public static bool operator ==(Vector3 left, Vector3 right) =>
            left.x == right.x && left.y == right.y && left.z == right.z;

        public static bool operator !=(Vector3 left, Vector3 right) => !(left == right);
        public override bool Equals(object? value) => value is Vector3 other && this == other;
        public override int GetHashCode() => HashCode.Combine(x, y, z);
    }

    internal struct Bounds
    {
        internal Bounds(Vector3 center, Vector3 size)
        {
            this.center = center;
            this.size = size;
        }

        internal Vector3 center;
        internal Vector3 size;
        internal Vector3 extents => new(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);

        internal void Encapsulate(Vector3 point)
        {
            Vector3 minimum = new(
                Math.Min(center.x - extents.x, point.x),
                Math.Min(center.y - extents.y, point.y),
                Math.Min(center.z - extents.z, point.z));
            Vector3 maximum = new(
                Math.Max(center.x + extents.x, point.x),
                Math.Max(center.y + extents.y, point.y),
                Math.Max(center.z + extents.z, point.z));
            center = new Vector3(
                (minimum.x + maximum.x) * 0.5f,
                (minimum.y + maximum.y) * 0.5f,
                (minimum.z + maximum.z) * 0.5f);
            size = new Vector3(
                maximum.x - minimum.x,
                maximum.y - minimum.y,
                maximum.z - minimum.z);
        }
    }

    internal static class Mathf
    {
        internal static float Max(float left, float right) => Math.Max(left, right);
        internal static float Lerp(float left, float right, float amount) => left + ((right - left) * amount);
    }

    internal static class Random
    {
        internal struct State
        {
            internal uint Value;
        }

        private static uint current = 1u;

        internal static State state
        {
            get => new() { Value = current };
            set => current = value.Value;
        }

        internal static void InitState(int seed)
        {
            current = unchecked((uint)seed);
        }

        internal static float value
        {
            get
            {
                current = unchecked((current * 1664525u) + 1013904223u);
                return (current & 0x00ffffffu) / 16777216f;
            }
        }
    }

    internal class Collider : Component
    {
        internal bool enabled = true;
        internal Bounds bounds => gameObject.activeInHierarchy
            ? new Bounds(Vector3.zero, Vector3.one)
            : default;
    }

    internal sealed class SphereCollider : Collider
    {
        internal Vector3 center;
        internal float radius;
    }

    internal sealed class CapsuleCollider : Collider
    {
        internal Vector3 center;
        internal float radius;
        internal float height;
        internal int direction;
    }

    internal sealed class BoxCollider : Collider
    {
        internal Vector3 center;
        internal Vector3 size;
    }

    internal sealed class MeshCollider : Collider
    {
        internal Mesh? sharedMesh;
    }

    internal sealed class Mesh : Object
    {
        internal Bounds bounds;
    }

    internal sealed class Sprite : Object { }
}

internal static class Utils
{
    internal static string GetPrefabName(UnityEngine.GameObject prefab) => prefab.name;
}

internal sealed class EffectList { }
internal sealed class Destructible : UnityEngine.Component { }
internal sealed class Plant : UnityEngine.Component { }

internal sealed class ItemDrop : UnityEngine.Component
{
    internal sealed class ItemData
    {
        internal SharedData m_shared = new();
        internal UnityEngine.Sprite Icon = new();
        internal UnityEngine.Sprite GetIcon() => Icon;
    }

    internal sealed class SharedData
    {
        internal PieceTable? m_buildPieces;
    }

    internal ItemData m_itemData = new();
}

internal sealed class PieceTable
{
    internal List<UnityEngine.GameObject> m_pieces = new();
    internal bool m_canRemovePieces;
}

internal sealed class Piece : UnityEngine.Component
{
    internal enum PieceCategory { Misc }

    internal sealed class Requirement
    {
        internal ItemDrop m_resItem = null!;
        internal int m_amount;
        internal bool m_recover;
    }

    internal string m_name = "";
    internal string m_description = "";
    internal UnityEngine.Sprite m_icon = null!;
    internal PieceCategory m_category;
    internal bool m_groundPiece;
    internal bool m_groundOnly;
    internal bool m_cultivatedGroundOnly;
    internal Heightmap.Biome m_onlyInBiome;
    internal bool m_canBeRemoved;
    internal bool m_targetNonPlayerBuilt;
    internal EffectList m_placeEffect = new();
    internal Requirement[] m_resources = Array.Empty<Requirement>();
    internal bool Removed { get; private set; }
    internal int DropResourcesCalls { get; private set; }

    private long creator;

    internal long GetCreator() => creator;
    internal bool CanBeRemoved() => true;

    internal (int Amount, string? ItemName) DropResources()
    {
        DropResourcesCalls++;
        int amount = 0;
        string? itemName = null;
        foreach (Requirement requirement in m_resources)
        {
            if (requirement.m_resItem == null || !requirement.m_recover)
            {
                continue;
            }

            amount += requirement.m_amount;
            itemName = requirement.m_resItem.gameObject.name;
        }

        return (amount, itemName);
    }

    internal void Destroy() => Removed = true;

    internal void SetCreator(long uid)
    {
        if (creator == 0L && gameObject.GetComponent<ZNetView>()?.IsOwner() == true)
        {
            creator = uid;
            gameObject.GetComponent<ZNetView>()!.GetZDO().Set(ZDOVars.s_creator, uid);
        }
    }
}

internal static class Heightmap
{
    internal enum Biome { None }
}

internal sealed class ObjectDB
{
    private readonly Dictionary<string, UnityEngine.GameObject> items = new();
    internal static ObjectDB? instance;
    internal void AddItemPrefab(UnityEngine.GameObject prefab) => items[prefab.name] = prefab;
    internal UnityEngine.GameObject? GetItemPrefab(string name) => items.TryGetValue(name, out UnityEngine.GameObject? item) ? item : null;
}

internal sealed class ZNetScene
{
    private readonly Dictionary<string, UnityEngine.GameObject> prefabs = new();
    internal void AddPrefab(UnityEngine.GameObject prefab) => prefabs[prefab.name] = prefab;
    internal UnityEngine.GameObject? GetPrefab(string name) => prefabs.TryGetValue(name, out UnityEngine.GameObject? prefab) ? prefab : null;
}

namespace BenheimQoL.Infrastructure
{
    internal static class Diagnostics
    {
        internal static readonly List<string> Events = new();
        internal static string Flatten(string value) => value;
        internal static void Event(string domain, string name, string fields) => Events.Add($"{domain}/{name}");
    }
}

namespace BenheimQoL
{
    internal static class Plugin
    {
        internal static readonly TestLog Log = new();
    }

    internal sealed class TestLog
    {
        internal readonly List<string> Errors = new();
        internal void LogError(object value) => Errors.Add(value.ToString() ?? "");
    }
}
