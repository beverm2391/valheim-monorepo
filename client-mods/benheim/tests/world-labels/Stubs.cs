using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace HarmonyLib
{
    public static class AccessTools
    {
        public static MethodInfo? DeclaredMethod(Type type, string name, Type[] parameters) =>
            type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                parameters,
                modifiers: null);

        public static FieldInfo? Field(Type? type, string name) =>
            type?.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        public static Type? Inner(Type type, string name) =>
            type.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);
    }
}

public sealed class DamageText : MonoBehaviour
{
    public enum TextType
    {
        Normal,
        Resistant,
        Weak,
        Immune,
        Heal,
        TooHard,
        Blocked,
        Bonus
    }

    private sealed class WorldTextInstance
    {
        public GameObject m_gui = null!;
        public TMP_Text m_textField = null!;
        public float m_duration = 0f;
    }

    public static DamageText instance = null!;
    public int m_largeFontSize = 16;
    public int m_smallFontSize = 8;
    public float m_smallFontDistance = 10f;
    public GameObject m_worldTextBase = null!;

    private readonly List<WorldTextInstance> m_worldTexts = new List<WorldTextInstance>();

    public int ActiveWorldTextCount => m_worldTexts.Count;
    public int CreatedBonusTextCount { get; private set; }
    public float LastWorldTextDuration => m_worldTexts[^1].m_duration;
    public string LastWorldText => m_worldTexts[^1].m_textField.text;

    private void AddInworldText(TextType type, Vector3 position, float distance, string text, bool mySelf)
    {
        _ = UnityEngine.Random.insideUnitSphere;
        GameObject gui = UnityEngine.Object.Instantiate(m_worldTextBase, transform);
        TMP_Text textField = gui.GetComponent<TMP_Text>()!;
        textField.color = type == TextType.Bonus
            ? new Color(1f, 0.63f, 0.24f, 1f)
            : new Color(1f, 1f, 1f, 1f);
        textField.fontSize = distance > m_smallFontDistance ? m_smallFontSize : m_largeFontSize;
        if (type == TextType.Bonus)
        {
            textField.fontSize *= 1.5f;
            CreatedBonusTextCount++;
        }

        textField.text = text;
        m_worldTexts.Add(new WorldTextInstance
        {
            m_gui = gui,
            m_textField = textField,
        });
    }
}

public static class Hud
{
    public static bool IsUserHidden() => false;
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
}

namespace TMPro
{
    public sealed class TMP_FontAsset : UnityEngine.Object
    {
    }

    public class TMP_Text : Component
    {
        public TMP_FontAsset? font;
        public Material? fontSharedMaterial;
        public Color color;
        public float fontSize;
        public bool richText = true;
        public bool raycastTarget = true;
        public string text = string.Empty;
        public string StyleMarker = string.Empty;
    }

    public sealed class TextMeshProUGUI : TMP_Text
    {
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
            MethodInfo? onDestroy = value.GetType().GetMethod(
                "OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            onDestroy?.Invoke(value, null);
            if (value is GameObject gameObject)
            {
                gameObject.SetActive(false);
            }
        }

        public static GameObject Instantiate(GameObject original, Transform parent) =>
            original.Clone(parent);
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

        public GameObject(string name = "")
        {
            this.name = name;
            transform = new Transform();
            Attach(transform);
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
            return components.Values.FirstOrDefault(component => type.IsAssignableFrom(component.GetType())) as T;
        }

        public void SetActive(bool active) => activeSelf = active;

        internal GameObject Clone(Transform parent)
        {
            GameObject clone = new GameObject(name + "(Clone)");
            TMP_Text? sourceText = GetComponent<TMP_Text>();
            if (sourceText != null)
            {
                TextMeshProUGUI clonedText = clone.AddComponent<TextMeshProUGUI>();
                clonedText.font = sourceText.font;
                clonedText.fontSharedMaterial = sourceText.fontSharedMaterial;
                clonedText.color = sourceText.color;
                clonedText.fontSize = sourceText.fontSize;
                clonedText.richText = sourceText.richText;
                clonedText.raycastTarget = sourceText.raycastTarget;
                clonedText.StyleMarker = sourceText.StyleMarker;
            }

            clone.transform.SetParent(parent, worldPositionStays: false);
            return clone;
        }

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

    public sealed class Camera : MonoBehaviour
    {
        public Vector3 ScreenPoint { get; set; } = new Vector3(960f, 540f, 1f);
        public Vector3 WorldToScreenPointScaled(Vector3 position) => ScreenPoint;
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

    public readonly struct Vector3
    {
        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero => new Vector3();
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public float sqrMagnitude => x * x + y * y + z * z;
        public static float Distance(Vector3 left, Vector3 right) =>
            (left - right).Magnitude;
        private float Magnitude => MathF.Sqrt(sqrMagnitude);
        public static Vector3 operator +(Vector3 left, Vector3 right) =>
            new Vector3(left.x + right.x, left.y + right.y, left.z + right.z);
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

    public static class Screen
    {
        public static int width = 1920;
        public static int height = 1080;
    }
}
