using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine
{
    internal class Object
    {
        internal string name = "";
        internal bool destroyRequested;
        internal static int GameObjectInstantiations { get; private set; }

        internal static T Instantiate<T>(T original, Transform parent) where T : Object
        {
            if (original is not GameObject source) throw new InvalidOperationException("Only marker GameObjects are instantiated here.");
            var clone = new GameObject(source.name + "(Clone)");
            Renderer? sourceRenderer = source.GetComponent<Renderer>();
            if (sourceRenderer != null) clone.AddComponent<Renderer>().sharedMaterial = sourceRenderer.sharedMaterial;
            clone.transform.SetParent(parent);
            GameObjectInstantiations++;
            return (T)(Object)clone;
        }

        internal static void Destroy(Object? value)
        {
            if (value == null) return;
            value.destroyRequested = true;
            if (value is GameObject gameObject) gameObject.SetActive(false);
        }

        internal static void ResetCounters() => GameObjectInstantiations = 0;
    }

    internal class Component : Object
    {
        internal GameObject gameObject = null!;
        internal Transform transform => gameObject.transform;
    }

    internal class Transform : Object
    {
        private readonly List<Transform> children = new();
        internal Transform(GameObject owner) { gameObject = owner; }
        internal GameObject gameObject { get; }
        internal Transform? parent { get; private set; }
        internal Vector3 position;
        internal Quaternion rotation;
        internal Vector3 lossyScale = new(1f, 1f, 1f);
        internal IReadOnlyList<Transform> Children => children;

        internal void SetParent(Transform value)
        {
            parent?.children.Remove(this);
            parent = value;
            value.children.Add(this);
        }
    }

    internal sealed class RectTransform : Transform
    {
        internal RectTransform(GameObject owner) : base(owner) { }
    }

    internal sealed class GameObject : Object
    {
        internal static readonly List<GameObject> Instances = new();
        private readonly Dictionary<Type, Component> components = new();
        private bool active = true;
        internal GameObject(string objectName) { name = objectName; transform = new Transform(this); Instances.Add(this); }
        internal Transform transform { get; }
        internal bool activeSelf => active;
        internal bool activeInHierarchy => active && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        internal IReadOnlyList<GameObject> Children
        {
            get
            {
                var result = new List<GameObject>();
                foreach (Transform child in transform.Children) result.Add(child.gameObject);
                return result;
            }
        }

        internal void SetActive(bool value) => active = value;
        internal T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObject = this };
            components[typeof(T)] = component;
            return component;
        }
        internal T GetComponent<T>() where T : Component => components.TryGetValue(typeof(T), out Component? value) ? (T)value : null!;
        internal T? GetComponentInChildren<T>(bool includeInactive) where T : Component
        {
            T? own = GetComponent<T>();
            if (own != null && (includeInactive || activeInHierarchy)) return own;
            foreach (Transform child in transform.Children)
            {
                T? found = child.gameObject.GetComponentInChildren<T>(includeInactive);
                if (found != null) return found;
            }
            return null;
        }
        internal static void ResetInstances() => Instances.Clear();
    }

    internal sealed class Renderer : Component { internal Material sharedMaterial = null!; }

    internal sealed class Material : Object
    {
        internal Material() { }
        internal Material(Material source) { name = source.name; color = source.color; HasColor = source.HasColor; }
        internal Color color;
        internal bool HasColor = true;
        internal bool HasProperty(string property) => property == "_Color" && HasColor;
    }

    internal readonly struct Color
    {
        internal Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        internal float r { get; }
        internal float g { get; }
        internal float b { get; }
        internal float a { get; }
        internal static Color green => new(0f, 1f, 0f);
        internal static Color white => new(1f, 1f, 1f);
    }

    internal struct Vector3
    {
        internal Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        internal float x;
        internal float y;
        internal float z;
        internal static Vector3 up => new(0f, 1f, 0f);
        internal static Vector3 down => new(0f, -1f, 0f);
        internal float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        internal Vector3 normalized { get { float length = magnitude; return length > 0f ? this * (1f / length) : new Vector3(); } }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 value, float scale) => new(value.x * scale, value.y * scale, value.z * scale);
        internal static Vector3 ProjectOnPlane(Vector3 vector, Vector3 normal) => vector - normal * Dot(vector, normal);
        internal static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
    }

    internal readonly struct Quaternion
    {
        private Quaternion(Vector3 forward) { Forward = forward; }
        internal Vector3 Forward { get; }
        internal static Quaternion LookRotation(Vector3 forward, Vector3 upwards) => new(forward);
    }

    internal static class Mathf { internal static float Abs(float value) => Math.Abs(value); }
    internal static class Time { internal static float unscaledTime; }
    internal enum KeyCode { F8 }
    internal static class Input { internal static bool F8Down; internal static bool GetKeyDown(KeyCode key) => key == KeyCode.F8 && F8Down; }
    internal static class Physics
    {
        internal static int Calls { get; private set; }
        internal static bool HitAvailable = true;
        internal static Func<float, float, float> Height = (_, _) => 0f;
        internal static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance, int layerMask)
        {
            Calls++;
            hit = new RaycastHit { point = new Vector3(origin.x, Height(origin.x, origin.z), origin.z), normal = Vector3.up };
            return HitAvailable;
        }
        internal static void Reset() { Calls = 0; HitAvailable = true; Height = (_, _) => 0f; }
    }
    internal struct RaycastHit { internal Vector3 point; internal Vector3 normal; }
}

namespace BenheimQoL.Infrastructure
{
    internal sealed class DiagnosticEvent
    {
        private DiagnosticEvent(string domain, string name) { Domain = domain; Name = name; }
        internal string Domain { get; }
        internal string Name { get; }
        internal Dictionary<string, object?> Fields { get; } = new(StringComparer.Ordinal);
        internal static DiagnosticEvent Create(string domain, string name) => new(domain, name);
        internal DiagnosticEvent String(string key, string value) { Fields[key] = value; return this; }
        internal DiagnosticEvent Boolean(string key, bool value) { Fields[key] = value; return this; }
        internal DiagnosticEvent Integer(string key, int value) { Fields[key] = value; return this; }
        internal DiagnosticEvent Number(string key, double value) { Fields[key] = value; return this; }
    }

    internal static class Diagnostics
    {
        internal static readonly List<DiagnosticEvent> Events = new();
        internal static void Emit(DiagnosticEvent value) => Events.Add(value);
        internal static void Clear() => Events.Clear();
    }
}

namespace BenheimQoL
{
    internal static class Plugin { internal static readonly TestLogger Log = new(); }
    internal sealed class TestLogger { internal void LogWarning(string message) { } }
}

internal static class HealthReporting { internal static bool GameplayActionsEnabled = true; }
internal static class ZInput { internal static bool F8Down; internal static bool GetKeyDown(UnityEngine.KeyCode key) => key == UnityEngine.KeyCode.F8 && F8Down; }
internal static class InputState { internal static bool TextEntryActive; internal static bool IsTextEntryActive() => TextEntryActive; }
internal sealed class Chat { internal static Chat? instance; internal bool HasFocus() => false; }
internal static class Menu { internal static bool IsVisible() => false; }
internal static class InventoryGui { internal static bool IsVisible() => false; }
internal static class Minimap { internal static bool IsOpen() => false; }
internal static class StoreGui { internal static bool IsVisible() => false; }
internal static class UnifiedPopup { internal static bool IsVisible() => false; }
internal static class Hud { internal static bool IsPieceSelectionVisible() => false; internal static bool InRadial() => false; }

internal sealed class MessageHud { internal enum MessageType { Center } }
internal sealed class Player
{
    internal static Player? m_localPlayer;
    internal readonly UnityEngine.GameObject gameObject = new("Player");
    internal UnityEngine.Transform transform => gameObject.transform;
    internal bool IsDead() => false;
    internal bool InCutscene() => false;
    internal bool IsTeleporting() => false;
    internal void Message(MessageHud.MessageType type, string message) { }
}

internal sealed class ZNetScene
{
    internal static ZNetScene? instance;
    private readonly Dictionary<string, UnityEngine.GameObject> prefabs = new(StringComparer.Ordinal);
    internal void AddPrefab(string key, UnityEngine.GameObject value) => prefabs[key] = value;
    internal UnityEngine.GameObject GetPrefab(string key) => prefabs.TryGetValue(key, out UnityEngine.GameObject? value) ? value : null!;
}

[Flags]
internal enum EffectAreaType { None = 0, PlayerBase = 1 }
internal sealed class EffectArea
{
    internal static class Type { internal const EffectAreaType PlayerBase = EffectAreaType.PlayerBase; }
    internal static readonly List<EffectArea> Areas = new();
    internal static int EnumerationCount { get; private set; }
    internal bool isActiveAndEnabled = true;
    internal EffectAreaType m_type = EffectAreaType.PlayerBase;
    internal UnityEngine.GameObject gameObject;
    private readonly int id;
    private readonly float radius;
    internal UnityEngine.Transform transform => gameObject.transform;
    internal EffectArea(int instanceId, float x, float z, float circleRadius, float y = 0f)
    {
        id = instanceId;
        radius = circleRadius;
        gameObject = new UnityEngine.GameObject($"EffectArea-{id}");
        gameObject.transform.position = new UnityEngine.Vector3(x, y, z);
    }
    internal int GetInstanceID() => id;
    internal float GetRadius() => radius;
    internal static IEnumerable<EffectArea> GetAllAreas() { EnumerationCount++; return Areas; }
    internal static void Reset() { Areas.Clear(); EnumerationCount = 0; }
}

internal sealed class CircleProjector : UnityEngine.Component
{
    internal UnityEngine.GameObject? m_prefab;
    internal LayerMask m_mask;
}
internal struct LayerMask { internal int value; }
internal static class Heightmap
{
    internal enum Biome { Meadows, AshLands }
    internal static Biome CurrentBiome = Biome.Meadows;
    internal static Biome FindBiome(UnityEngine.Vector3 position) => CurrentBiome;
}

namespace BenheimQoL.SpawnProtection
{
    internal static class SpawnProtectionMinimapIndicator
    {
        internal static int UpdateCount;
        internal static int ResetCount;
        internal static void Update() => UpdateCount++;
        internal static void Reset() => ResetCount++;
        internal static void Clear() { UpdateCount = 0; ResetCount = 0; }
    }
}

namespace TMPro
{
    internal sealed class TMP_Text
    {
        internal float fontSize;
        internal UnityEngine.Color color;
        internal string text = "";
    }
}

namespace UnityEngine.UI
{
    internal sealed class Toggle
    {
        internal sealed class ToggleEvent
        {
            private readonly List<Action<bool>> listeners = new();
            internal void AddListener(Action<bool> callback) => listeners.Add(callback);
            internal void Invoke(bool value) { foreach (Action<bool> listener in listeners) listener(value); }
        }
        internal ToggleEvent onValueChanged = new();
        internal bool isOn { get; private set; }
        internal void SetIsOnWithoutNotify(bool value) => isOn = value;
    }
}

namespace BenheimQoL.Shortcuts
{
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    internal sealed class NativeTemplates
    {
        internal TMP_Text Text { get; } = new();
        internal Toggle Checkbox { get; } = new();
    }

    internal static partial class ShortcutOverlay
    {
        private static readonly Color ConfigAccent = Color.green;
        private static void AddSectionHeading(RectTransform parent, string text, Color color, TMP_Text template) { }
        private static TMP_Text CreateText(string name, RectTransform parent, TMP_Text template, bool layoutElement) => new();
        private static Toggle AddConfigToggle(RectTransform parent, NativeTemplates templates, string label, bool value, Action<bool> changed)
        {
            var toggle = new Toggle();
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(changed);
            return toggle;
        }
        internal static Toggle TestBuildSpawnProtectionConfig()
        {
            BuildSpawnProtectionConfig(new RectTransform(new GameObject("ConfigRoot")), new NativeTemplates());
            return spawnProtectionToggle!;
        }
        internal static void TestRefreshSpawnProtectionConfig() => RefreshSpawnProtectionConfig();
    }
}
