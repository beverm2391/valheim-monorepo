using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class Sign : MonoBehaviour
{
    public TextMeshProUGUI m_textWidget = null!;
}

public sealed class Piece : MonoBehaviour
{
    public string m_name = string.Empty;
}

public sealed class TeleportWorld : MonoBehaviour
{
    public MeshRenderer? m_model;
    public string Tag { get; set; } = string.Empty;
    public string GetText() => Tag;
}

public sealed class ZNetView : MonoBehaviour
{
}

public sealed class ZNetScene
{
    public static ZNetScene? instance;
    public List<GameObject> m_prefabs { get; } = new();
    public List<GameObject> m_nonNetViewPrefabs { get; } = new();
}

public static class Plugin
{
    public static TestLogger Log { get; } = new();
}

public sealed class TestLogger
{
    public List<string> Infos { get; } = new();
    public List<string> Warnings { get; } = new();
    public void LogInfo(string message) => Infos.Add(message);
    public void LogWarning(string message) => Warnings.Add(message);
    public void Clear()
    {
        Infos.Clear();
        Warnings.Clear();
    }
}

namespace BenheimQoL.WorldLabels
{
    internal sealed class SignGlowController : MonoBehaviour
    {
        internal void Initialize(Sign sign) { }
        internal void RestoreAndRemove() => Destroy(this);
        private void OnDestroy() => WorldLabelRuntime.Forget(this);
    }
}

namespace UnityEngine
{
    public class Object
    {
        private static int nextId;
        public string name = string.Empty;
        public HideFlags hideFlags { get; set; }
        public bool Destroyed { get; private set; }
        private int InstanceId { get; } = ++nextId;

        public static bool operator ==(Object? left, Object? right)
        {
            bool leftNull = ReferenceEquals(left, null) || left.Destroyed;
            bool rightNull = ReferenceEquals(right, null) || right.Destroyed;
            return leftNull ? rightNull : !rightNull && ReferenceEquals(left, right);
        }

        public static bool operator !=(Object? left, Object? right) => !(left == right);
        public static implicit operator bool(Object? value) => value != null;
        public override bool Equals(object? value) => this == value as Object;
        public override int GetHashCode() => InstanceId;

        public static void Destroy(Object value)
        {
            if (value.Destroyed)
            {
                return;
            }

            value.Destroyed = true;
            if (value is GameObject gameObject)
            {
                foreach (Component component in gameObject.Components.ToArray())
                {
                    Destroy(component);
                }
                gameObject.SetActive(false);
                return;
            }

            MethodInfo? onDestroy = value.GetType().GetMethod(
                "OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            onDestroy?.Invoke(value, null);
        }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; } = null!;
        public Transform transform => gameObject.transform;
        public T? GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T? GetComponentInParent<T>() where T : Component
        {
            for (Transform? current = transform; current != null; current = current.parent)
            {
                T? component = current.gameObject.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }
            }
            return null;
        }
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : Component =>
            gameObject.GetComponentsInChildren<T>(includeInactive);
    }

    public class MonoBehaviour : Component
    {
        public bool enabled = true;
        protected void InvokeRepeating(string methodName, float time, float repeatRate) { }
        protected void CancelInvoke(string methodName) { }
    }

    public sealed class GameObject : Object
    {
        private readonly List<Component> components = new();

        public GameObject(string name = "", params Type[] componentTypes)
        {
            this.name = name;
            Type transformType = componentTypes.Contains(typeof(RectTransform))
                ? typeof(RectTransform)
                : typeof(Transform);
            transform = (Transform)Activator.CreateInstance(transformType)!;
            Attach(transform);
            foreach (Type type in componentTypes.Where(type => type != typeof(Transform) && type != typeof(RectTransform)))
            {
                AddComponent(type);
            }
        }

        public bool activeSelf { get; private set; } = true;
        public Transform transform { get; }
        internal IEnumerable<Component> Components => components;

        public T AddComponent<T>() where T : Component => (T)AddComponent(typeof(T));
        public Component AddComponent(Type type)
        {
            Component component = (Component)Activator.CreateInstance(type, nonPublic: true)!;
            Attach(component);
            return component;
        }

        public T? GetComponent<T>() where T : Component =>
            components.OfType<T>().FirstOrDefault();

        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : Component
        {
            List<T> result = new();
            Collect(this, result, includeInactive);
            return result.ToArray();
        }

        public void SetActive(bool active) => activeSelf = active;

        private static void Collect<T>(GameObject current, List<T> result, bool includeInactive)
            where T : Component
        {
            if (includeInactive || current.activeSelf)
            {
                result.AddRange(current.components.OfType<T>());
            }
            for (int index = 0; index < current.transform.childCount; index++)
            {
                Collect(current.transform.GetChild(index).gameObject, result, includeInactive);
            }
        }

        private void Attach(Component component)
        {
            component.gameObject = this;
            component.name = name;
            components.Add(component);
        }
    }

    public class Transform : Component
    {
        private readonly List<Transform> children = new();
        private Transform? parentValue;
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
        public Transform? parent => parentValue;
        public int childCount => children.Count;

        public Vector3 position
        {
            get => parent == null
                ? localPosition
                : parent.position + parent.rotation * Scale(localPosition, parent.lossyScale);
            set => localPosition = parent == null
                ? value
                : Divide(Quaternion.Inverse(parent.rotation) * (value - parent.position), parent.lossyScale);
        }

        public Quaternion rotation
        {
            get => parent == null ? localRotation : parent.rotation * localRotation;
            set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value;
        }

        public Vector3 lossyScale => parent == null ? localScale : Scale(localScale, parent.lossyScale);

        public void SetParent(Transform newParent, bool worldPositionStays)
        {
            Vector3 oldPosition = position;
            Quaternion oldRotation = rotation;
            parentValue?.children.Remove(this);
            parentValue = newParent;
            newParent.children.Add(this);
            if (worldPositionStays)
            {
                position = oldPosition;
                rotation = oldRotation;
            }
        }

        public Transform GetChild(int index) => children[index];
        public Vector3 InverseTransformPoint(Vector3 point) =>
            Divide(Quaternion.Inverse(rotation) * (point - position), lossyScale);

        private static Vector3 Scale(Vector3 left, Vector3 right) =>
            new(left.x * right.x, left.y * right.y, left.z * right.z);
        private static Vector3 Divide(Vector3 left, Vector3 right) =>
            new(left.x / right.x, left.y / right.y, left.z / right.z);
    }

    public sealed class RectTransform : Transform
    {
        public Vector2 sizeDelta;
        public Vector2 pivot = new(0.5f, 0.5f);
        public Rect rect => new(sizeDelta.x, sizeDelta.y);
    }

    public sealed class Mesh
    {
        public Bounds bounds;
    }

    public sealed class MeshFilter : Component
    {
        public Mesh? sharedMesh;
    }

    public class Renderer : Component
    {
        public bool enabled = true;
        public ShadowCastingMode shadowCastingMode;
        public bool receiveShadows;
        public LightProbeUsage lightProbeUsage;
        public ReflectionProbeUsage reflectionProbeUsage;
        public MotionVectorGenerationMode motionVectorGenerationMode;
        public bool allowOcclusionWhenDynamic;
    }

    public sealed class MeshRenderer : Renderer
    {
        public Material[] sharedMaterials = Array.Empty<Material>();
        public Bounds? ExplicitBounds { get; set; }
        public Bounds bounds
        {
            get
            {
                if (ExplicitBounds.HasValue)
                {
                    return ExplicitBounds.Value;
                }
                Bounds local = GetComponent<MeshFilter>()?.sharedMesh?.bounds ?? new Bounds(Vector3.zero, Vector3.zero);
                return new Bounds(local.min + transform.position, local.max + transform.position);
            }
        }
    }

    public class Collider : Component
    {
    }

    public sealed class Canvas : Component
    {
        public RenderMode renderMode;
        public int sortingLayerID;
        public int sortingOrder;
    }

    public sealed class CanvasRenderer : Component
    {
    }

    public enum RenderMode { WorldSpace }

    public sealed class Material : Object
    {
        public Material() { }
        public Material(Material source) => name = source.name;
        public void EnableKeyword(string keyword) { }
        public bool HasProperty(string property) => true;
        public void SetColor(string property, Color value) { }
        public void SetFloat(string property, float value) { }
    }

    public static class Mathf
    {
        public static float Max(float left, float right) => MathF.Max(left, right);
    }

    public struct Bounds
    {
        public Bounds(Vector3 min, Vector3 max)
        {
            this.min = min;
            this.max = max;
        }
        public Vector3 min;
        public Vector3 max;
        public void Encapsulate(Bounds bounds)
        {
            min = new Vector3(
                MathF.Min(min.x, bounds.min.x),
                MathF.Min(min.y, bounds.min.y),
                MathF.Min(min.z, bounds.min.z));
            max = new Vector3(
                MathF.Max(max.x, bounds.max.x),
                MathF.Max(max.y, bounds.max.y),
                MathF.Max(max.z, bounds.max.z));
        }
    }

    public readonly struct Quaternion
    {
        public Quaternion(float yDegrees) => this.yDegrees = Normalize(yDegrees);
        public readonly float yDegrees;
        public static Quaternion identity => new(0f);
        public static Quaternion Euler(float x, float y, float z) => new(y);
        public static Quaternion Inverse(Quaternion value) => new(-value.yDegrees);
        public static Quaternion operator *(Quaternion left, Quaternion right) =>
            new(left.yDegrees + right.yDegrees);
        public static Vector3 operator *(Quaternion rotation, Vector3 vector)
        {
            float radians = rotation.yDegrees * MathF.PI / 180f;
            float cosine = MathF.Cos(radians);
            float sine = MathF.Sin(radians);
            return new Vector3(
                vector.x * cosine + vector.z * sine,
                vector.y,
                -vector.x * sine + vector.z * cosine);
        }
        public static bool operator ==(Quaternion left, Quaternion right) =>
            MathF.Abs(left.yDegrees - right.yDegrees) < 0.0001f;
        public static bool operator !=(Quaternion left, Quaternion right) => !(left == right);
        public override bool Equals(object? value) => value is Quaternion other && this == other;
        public override int GetHashCode() => yDegrees.GetHashCode();
        private static float Normalize(float value)
        {
            value %= 360f;
            return value < 0f ? value + 360f : value;
        }
    }

    public readonly struct Vector2
    {
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public readonly float x;
        public readonly float y;
    }

    public readonly struct Vector3
    {
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0f, 0f, 0f);
        public static Vector3 one => new(1f, 1f, 1f);
        public static Vector3 up => new(0f, 1f, 0f);
        public readonly float x;
        public readonly float y;
        public readonly float z;
        public static Vector3 operator +(Vector3 left, Vector3 right) => new(left.x + right.x, left.y + right.y, left.z + right.z);
        public static Vector3 operator -(Vector3 left, Vector3 right) => new(left.x - right.x, left.y - right.y, left.z - right.z);
        public static Vector3 operator *(Vector3 value, float scale) => new(value.x * scale, value.y * scale, value.z * scale);
    }

    public readonly struct Vector4
    {
        public Vector4(float x, float y, float z, float w) { }
    }

    public readonly struct Rect
    {
        public Rect(float width, float height) { this.width = width; this.height = height; }
        public readonly float width;
        public readonly float height;
        public Vector2 size => new(width, height);
    }

    public readonly struct Color
    {
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new(1f, 1f, 1f, 1f);
        public readonly float r;
        public readonly float g;
        public readonly float b;
        public readonly float a;
        public static bool operator ==(Color left, Color right) =>
            left.r == right.r && left.g == right.g && left.b == right.b && left.a == right.a;
        public static bool operator !=(Color left, Color right) => !(left == right);
        public override bool Equals(object? value) => value is Color other && this == other;
        public override int GetHashCode() => HashCode.Combine(r, g, b, a);
    }

    public enum HideFlags { DontSave }
}
