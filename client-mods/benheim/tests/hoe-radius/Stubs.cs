using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

public sealed class TerrainOp
{
    public static bool m_forceDisableTerrainOps;
    public UnityEngine.GameObject gameObject = new("terrain_op");
    public Settings m_settings = new();

    public sealed class Settings
    {
        public float m_levelOffset;
        public bool m_level;
        public float m_levelRadius = 2f;
        public bool m_square = true;
        public bool m_raise;
        public float m_raiseRadius = 2f;
        public float m_raisePower;
        public float m_raiseDelta;
        public bool m_smooth;
        public float m_smoothRadius = 2f;
        public float m_smoothPower = 3f;
        public bool m_paintCleared = true;
        public bool m_paintHeightCheck;
        public TerrainModifier.PaintType m_paintType;
        public float m_paintRadius = 2f;
        public float m_paintStrength = 1f;
        public float m_paintExp = 0.1f;
        public UnityEngine.AnimationCurve? m_paintCurve;
        public bool m_rotation;
        public bool m_sides;
        public float m_addMedianMax;
        public float m_centerMultiplicationFactor;
        public float m_pointMultiplicationFactor;
        public bool m_halfOffset = true;

        public float GetRadius()
        {
            float result = 0f;
            if (m_level) result = Math.Max(result, m_levelRadius);
            if (m_raise) result = Math.Max(result, m_raiseRadius);
            if (m_smooth) result = Math.Max(result, m_smoothRadius);
            if (m_paintCleared) result = Math.Max(result, m_paintRadius);
            return result;
        }
    }
}

public static class TerrainModifier
{
    public enum PaintType { Dirt, Cultivate, Paved }
}

public sealed class Piece
{
    public UnityEngine.GameObject gameObject = new("piece");
}

public sealed class ItemDrop
{
    public sealed class ItemData
    {
        public UnityEngine.GameObject? m_dropPrefab;
    }
}

public sealed class Player
{
    public static Player? m_localPlayer;
    public ItemDrop.ItemData? RightItem;
    public Piece? SelectedPiece;
    public bool PlaceMode;
    public UnityEngine.GameObject? m_placementGhost;
    public bool InPlaceMode() => PlaceMode;
    public Piece? GetSelectedPiece() => SelectedPiece;
}

public sealed class ZPackage
{
    private readonly MemoryStream stream = new();
    private readonly BinaryWriter writer;
    private readonly BinaryReader reader;

    public ZPackage()
    {
        writer = new BinaryWriter(stream);
        reader = new BinaryReader(stream);
    }

    public void Write(int value) => writer.Write(value);
    public void Write(string value) => writer.Write(value);
    public int ReadInt() => reader.ReadInt32();
    public string ReadString() => reader.ReadString();
    public int Size() { writer.Flush(); return (int)stream.Length; }
    public int GetPos() => (int)stream.Position;
    public void SetPos(int value) => stream.Position = value;
}

public static class Utils
{
    public static string GetPrefabName(string name) => name.EndsWith("(Clone)", StringComparison.Ordinal)
        ? name.Substring(0, name.Length - "(Clone)".Length)
        : name;
}

namespace UnityEngine
{
    public sealed class AnimationCurve { }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0f, 0f, 0f);
        public static Vector3 one => new(1f, 1f, 1f);
        public static Vector3 Scale(Vector3 left, Vector3 right) =>
            new(left.x * right.x, left.y * right.y, left.z * right.z);
        public static Vector3 operator *(Vector3 value, float scale) => new(value.x * scale, value.y * scale, value.z * scale);
        public static float Distance(Vector3 left, Vector3 right)
        {
            float x = left.x - right.x;
            float y = left.y - right.y;
            float z = left.z - right.z;
            return MathF.Sqrt(x * x + y * y + z * z);
        }
    }

    public sealed class Transform
    {
        private readonly Dictionary<string, Transform> children = new(StringComparer.Ordinal);
        private ParticleSystem? particleSystem;
        public Vector3 localScale = new(1f, 1f, 1f);
        public Transform? parent;
        public Vector3 lossyScale => parent == null ? localScale : Vector3.Scale(parent.lossyScale, localScale);
        public Transform? Find(string name) => children.TryGetValue(name, out Transform? child) ? child : null;
        public T? GetComponentInChildren<T>() where T : class => particleSystem as T;
        public void AddComponentInChildren(ParticleSystem value)
        {
            particleSystem = value;
            value.transform.parent = this;
        }
        public Transform Add(string name, Vector3 scale)
        {
            var child = new Transform { localScale = scale, parent = this };
            children.Add(name, child);
            return child;
        }
    }

    public sealed class ParticleSystem
    {
        public ParticleSystem(Vector3 scale) { transform.localScale = scale; }
        public Transform transform { get; } = new();
    }

    public sealed class GameObject
    {
        private readonly Dictionary<Type, object> components = new();
        public GameObject(string name) { this.name = name; }
        public string name;
        public Transform transform { get; } = new();
        public void AddComponent<T>(T value) where T : class => components[typeof(T)] = value;
        public T? GetComponent<T>() where T : class => components.TryGetValue(typeof(T), out object? value) ? (T)value : null;
    }
}

namespace BenheimQoL.Infrastructure
{
    internal static class InputState
    {
        internal static bool LeftShiftHeld;
        internal static bool IsLeftShiftHeld() => LeftShiftHeld;
    }

    internal static class Diagnostics
    {
        internal static readonly List<DiagnosticEvent> Events = new();
        internal static void Emit(DiagnosticEvent value)
        {
            value.Prepare(DateTime.UtcNow, "test", "test");
            Events.Add(value);
        }
    }
}

namespace BenheimQoL.Farming
{
    internal static class FarmingReflection
    {
        internal static readonly FieldInfo PlacementGhostField = typeof(Player).GetField(nameof(Player.m_placementGhost))!;
        internal static ItemDrop.ItemData? GetRightItem(Player player) => player.RightItem;
    }
}
