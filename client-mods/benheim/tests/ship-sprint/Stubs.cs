using TMPro;
using UnityEngine;

public sealed class Ship : Object
{
    internal Rigidbody? Body { get; set; }

    internal bool LocalRequestActive { get; set; }

    public enum Speed
    {
        Stop,
        Back,
        Slow,
        Half,
        Full
    }

    public T? GetComponent<T>() where T : class => Body as T;
}

public sealed class Player : Object
{
    public static Player? m_localPlayer;

    internal Ship? ControlledShip { get; set; }

    public Ship? GetControlledShip() => ControlledShip;
}

public sealed class Hud : Object
{
    internal bool Visible { get; set; } = true;

    public GameObject m_shipHudRoot = new GameObject();

    public GameObject m_shipControlsRoot = new GameObject();

    public RectTransform m_shipWindIndicatorRoot = new RectTransform();

    public TMP_Text m_healthText = new TMP_Text();

    public bool IsVisible() => Visible;
}

public sealed class ZNet
{
    public static ZNet? instance;
}

public sealed class ZNetScene
{
    public static ZNetScene? instance;
}

namespace BenheimQoL.ShipSprint
{
    internal static class ShipSprintRuntime
    {
        internal static Vector3 GaugeVelocity(Ship ship, Rigidbody body) => body.linearVelocity;

        internal static bool IsLocalRequestActive(Ship ship) => ship.LocalRequestActive;
    }
}

namespace TMPro
{
    public enum TextAlignmentOptions
    {
        Center
    }

    public enum FontStyles
    {
        Normal
    }

    public sealed class TMP_Text : Object
    {
        public TMP_Text()
        {
            rectTransform = new RectTransform();
            gameObject = new GameObject(rectTransform);
        }

        public string text = string.Empty;
        public TextAlignmentOptions alignment;
        public FontStyles fontStyle;
        public float fontSize = 20f;
        public bool enableAutoSizing;
        public bool raycastTarget;
        public GameObject gameObject { get; }
        public RectTransform rectTransform { get; }
        public Transform transform => rectTransform;
    }
}

namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed { get; private set; }

        public string name = string.Empty;

        internal static TMP_Text? LastInstantiated { get; private set; }

        public static implicit operator bool(Object? value) => value is not null && !value.Destroyed;

        public static bool operator !(Object? value) => value is null || value.Destroyed;

        public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays)
            where T : Object
        {
            if (original is not TMP_Text donor)
            {
                throw new System.NotSupportedException(typeof(T).FullName);
            }

            TMP_Text clone = new TMP_Text
            {
                fontSize = donor.fontSize
            };
            clone.rectTransform.localRotation = donor.rectTransform.localRotation;
            clone.rectTransform.parent = parent;
            LastInstantiated = clone;
            return (T)(Object)clone;
        }

        public static void Destroy(Object value)
        {
            value.Destroyed = true;
            if (value is GameObject gameObject)
            {
                gameObject.SetActive(false);
            }
        }
    }

    public sealed class GameObject : Object
    {
        public GameObject()
            : this(new Transform())
        {
        }

        internal GameObject(Transform transform)
        {
            this.transform = transform;
            transform.gameObject = this;
        }

        public bool activeSelf { get; private set; } = true;

        public Transform transform { get; }

        public void SetActive(bool active) => activeSelf = active;
    }

    public class Transform : Object
    {
        public GameObject? gameObject { get; internal set; }
        public Transform? parent { get; set; }
        public Quaternion localRotation { get; set; } = Quaternion.identity;
    }

    public sealed class RectTransform : Transform
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Rect rect => new Rect(sizeDelta.x, sizeDelta.y);
    }

    public sealed class Rigidbody : Object
    {
        public Vector3 linearVelocity;
    }

    public readonly struct Rect
    {
        internal Rect(float width, float height)
        {
            this.width = width;
            this.height = height;
        }

        public float width { get; }
        public float height { get; }
    }

    public readonly struct Vector2
    {
        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public float x { get; }
        public float y { get; }
    }

    public readonly struct Vector3
    {
        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public float x { get; }
        public float y { get; }
        public float z { get; }
    }

    public readonly struct Quaternion
    {
        private Quaternion(float zDegrees) => ZDegrees = zDegrees;

        internal float ZDegrees { get; }

        public static Quaternion identity => new Quaternion(0f);

        public static Quaternion Euler(float x, float y, float z) => new Quaternion(z);
    }
}
