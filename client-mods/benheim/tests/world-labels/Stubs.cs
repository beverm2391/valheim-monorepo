using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

public sealed class ZNetScene
{
    public static ZNetScene? instance;
    public readonly List<GameObject> m_prefabs = new List<GameObject>();
    public readonly List<GameObject> m_nonNetViewPrefabs = new List<GameObject>();
}

public sealed class Sign : MonoBehaviour
{
    public TextMeshProUGUI m_textWidget = null!;
}

public sealed class TeleportWorld : MonoBehaviour
{
    public MeshRenderer? m_model;
    public string Tag { get; set; } = string.Empty;
    public string GetText() => Tag;
}

public sealed class Player : MonoBehaviour
{
    public static Player? m_localPlayer;
}

public static class Utils
{
    public static Camera? MainCamera { get; set; }
    public static Camera? GetMainCamera() => MainCamera;
}

public sealed class Billboard : MonoBehaviour
{
    public bool m_vertical;
    public bool m_invert;
}

public static class Plugin
{
    public static TestLogger Log { get; } = new TestLogger();
}

public sealed class TestLogger
{
    public List<string> Infos { get; } = new List<string>();
    public List<string> Warnings { get; } = new List<string>();

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
        private Sign sign = null!;
        private Material originalMaterial = null!;
        private Material glowMaterial = null!;

        internal void Initialize(Sign source)
        {
            sign = source;
            originalMaterial = source.m_textWidget.fontSharedMaterial!;
            glowMaterial = new Material();
            source.m_textWidget.fontSharedMaterial = glowMaterial;
        }

        internal void RestoreAndRemove()
        {
            if (sign.m_textWidget.fontSharedMaterial == glowMaterial)
            {
                sign.m_textWidget.fontSharedMaterial = originalMaterial;
            }

            Destroy(glowMaterial);
            Destroy(this);
        }

        private void OnDestroy() => WorldLabelRuntime.Forget(this);
    }

    internal static class WorldLabelStyle
    {
        internal static Color PortalAmber => new Color(1f, 0.5f, 0.1f, 1f);
    }
}

namespace TMPro
{
    public enum TextAlignmentOptions
    {
        Center
    }

    public enum TextWrappingModes
    {
        NoWrap
    }

    public sealed class TMP_FontAsset : UnityEngine.Object
    {
    }

    public sealed class TextMeshProUGUI : Component
    {
        public TMP_FontAsset? font;
        public Material? fontSharedMaterial;
        public Color color;
        public float fontSize;
        public TextAlignmentOptions alignment;
        public TextWrappingModes textWrappingMode;
        public bool richText = true;
        public bool raycastTarget = true;
        public string text = string.Empty;
    }
}

namespace UnityEngine
{
    public class Object
    {
        private static int nextInstanceId;

        public string name = string.Empty;
        public bool Destroyed { get; private set; }
        private int InstanceId { get; } = ++nextInstanceId;

        public int GetInstanceID() => InstanceId;

        public static bool operator ==(Object? left, Object? right)
        {
            bool leftNull = ReferenceEquals(left, null) || left.Destroyed;
            bool rightNull = ReferenceEquals(right, null) || right.Destroyed;
            return leftNull ? rightNull : !rightNull && ReferenceEquals(left, right);
        }

        public static bool operator !=(Object? left, Object? right) => !(left == right);
        public override bool Equals(object? value) => this == value as Object;
        public override int GetHashCode() => InstanceId;

        public static void Destroy(Object value)
        {
            if (value.Destroyed)
            {
                return;
            }

            value.Destroyed = true;
            MethodInfo? onDestroy = value.GetType().GetMethod(
                "OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            onDestroy?.Invoke(value, null);
            if (value is GameObject gameObject)
            {
                gameObject.SetActive(false);
            }
        }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; } = null!;
        public Transform transform => gameObject.transform;
        public T? GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }

    public class MonoBehaviour : Component
    {
        public bool enabled = true;
        protected void InvokeRepeating(string methodName, float time, float repeatRate) { }
        protected void CancelInvoke(string methodName) { }
    }

    public sealed class GameObject : Object
    {
        private readonly Dictionary<Type, Component> components =
            new Dictionary<Type, Component>();
        private readonly Dictionary<Type, int> componentLookups =
            new Dictionary<Type, int>();

        public GameObject(string name = "", params Type[] componentTypes)
        {
            this.name = name;
            transform = Array.Exists(componentTypes, type => type == typeof(RectTransform))
                ? new RectTransform()
                : new Transform();
            Attach(transform);
            foreach (Type type in componentTypes)
            {
                if (type == typeof(Transform) || type == typeof(RectTransform))
                {
                    continue;
                }

                Attach((Component)Activator.CreateInstance(type)!);
            }
        }

        public bool activeSelf { get; private set; } = true;
        public HideFlags hideFlags { get; set; }
        public Transform transform { get; }

        public T AddComponent<T>() where T : Component
        {
            T component = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
            Attach(component);
            return component;
        }

        public T? GetComponent<T>() where T : Component
        {
            Type type = typeof(T);
            componentLookups[type] = GetComponentLookupCount<T>() + 1;
            return components.TryGetValue(typeof(T), out Component? component)
                ? (T)component
                : null;
        }

        public int GetComponentLookupCount<T>() where T : Component =>
            componentLookups.TryGetValue(typeof(T), out int count) ? count : 0;

        public void SetActive(bool active) => activeSelf = active;

        private void Attach(Component component)
        {
            component.gameObject = this;
            component.name = name;
            components[component.GetType()] = component;
        }
    }

    public class Transform : Component
    {
        public Vector3 position;
        public Vector3 localScale;
        public Transform? parent { get; private set; }

        public void SetParent(Transform newParent, bool worldPositionStays) => parent = newParent;

        public bool IsChildOf(Transform possibleParent)
        {
            for (Transform? current = parent; current != null; current = current.parent)
            {
                if (current == possibleParent)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class RectTransform : Transform
    {
        public Vector2 sizeDelta;
    }

    public sealed class Canvas : MonoBehaviour
    {
        public RenderMode renderMode;
        public Camera? worldCamera;
    }

    public enum RenderMode
    {
        WorldSpace
    }

    public sealed class Camera : MonoBehaviour
    {
    }

    public sealed class MeshRenderer : Object
    {
        public Bounds bounds;
    }

    public sealed class Material : Object
    {
    }

    public readonly struct Bounds
    {
        public Vector3 max { get; init; }
    }

    public readonly struct Color
    {
        public Color(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public readonly float r;
        public readonly float g;
        public readonly float b;
        public readonly float a;
    }

    public readonly struct Vector2
    {
        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public readonly float x;
        public readonly float y;
    }

    public readonly struct Vector3
    {
        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero => new Vector3();
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public float sqrMagnitude => x * x + y * y + z * z;
        public static Vector3 operator -(Vector3 left, Vector3 right) =>
            new Vector3(left.x - right.x, left.y - right.y, left.z - right.z);
        public static Vector3 operator *(Vector3 value, float scale) =>
            new Vector3(value.x * scale, value.y * scale, value.z * scale);

        public readonly float x;
        public readonly float y;
        public readonly float z;
    }

    public enum QueryTriggerInteraction
    {
        Ignore
    }

    public struct RaycastHit
    {
        public Transform? transform;
    }

    public static class Physics
    {
        public const int DefaultRaycastLayers = -5;
        public static bool NextLinecastHit { get; set; }
        public static Transform? NextHitTransform { get; set; }

        public static bool Linecast(
            Vector3 start,
            Vector3 end,
            out RaycastHit hit,
            int layerMask,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            hit = new RaycastHit { transform = NextHitTransform };
            return NextLinecastHit;
        }
    }

    public enum HideFlags
    {
        DontSave
    }
}
