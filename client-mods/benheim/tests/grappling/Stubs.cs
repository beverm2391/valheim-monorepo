using System;
using System.Collections.Generic;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute
    {
        internal HarmonyPatch(Type type, string methodName) { }
    }
}

namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] Object? value) => value != null;
    }

    public class MonoBehaviour : Object
    {
        public Transform transform { get; } = new Transform();
    }

    public sealed class Transform
    {
        public Vector3 position;
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0f, 0f, 0f);

        public static float Distance(Vector3 left, Vector3 right)
        {
            float x = left.x - right.x;
            float y = left.y - right.y;
            float z = left.z - right.z;
            return (float)Math.Sqrt(x * x + y * y + z * z);
        }
    }

    public sealed class Sprite : Object { }

    public sealed class GameObject : Object
    {
        private readonly Dictionary<Type, Object> components = new Dictionary<Type, Object>();
        public GameObject(string name = "fixture") { }

        public T AddComponent<T>(T component) where T : Object
        {
            components[typeof(T)] = component;
            return component;
        }

        public T? GetComponent<T>() where T : Object =>
            components.TryGetValue(typeof(T), out Object? value) ? (T)value : null;
    }
}

public class Character : UnityEngine.MonoBehaviour
{
    public bool Dead;
    public bool Teleporting;
    public bool IsDead() => Dead;
    public bool IsTeleporting() => Teleporting;
}

public sealed class Player : Character
{
    public enum ActionKind { Other, Reload }

    public sealed class MinorActionData
    {
        public enum ActionType { Other, Reload }
        public ActionType m_type;
        public ItemDrop.ItemData? m_item;
        public float m_duration;
    }

    public static Player m_localPlayer = null!;
    public float SkillLevel;
    public float Health = 100f;
    private readonly Skills skills = new Skills();
    internal readonly List<float> RaisedExperience = new List<float>();

    public float GetSkillLevel(Skills.SkillType type) => SkillLevel;
    public Skills GetSkills() => skills;
    public void RaiseSkill(Skills.SkillType type, float amount) => RaisedExperience.Add(amount);
}

public sealed class Projectile : UnityEngine.MonoBehaviour
{
    public float m_ttl = 4f;
    public UnityEngine.GameObject m_spawnOnHit = null!;
}

public sealed class GrapplingPoint : UnityEngine.MonoBehaviour
{
    public float m_maxLength = 50f;
}

public sealed class Attack
{
    public UnityEngine.GameObject m_spawnOnHit = null!;
    public UnityEngine.GameObject m_attackProjectile = null!;
    public bool m_requiresReload;
}

public static class Localization
{
    public static string Translate(string word) => word;
}

public sealed class ZNetView : UnityEngine.MonoBehaviour
{
    public bool Owner = true;
    public bool IsOwner() => Owner;
}

public sealed class ItemDrop
{
    public sealed class ItemData
    {
        public SharedData m_shared = new SharedData();

        public sealed class SharedData
        {
            public Attack m_attack = new Attack();
            public Attack m_secondaryAttack = new Attack();
        }
    }
}

public sealed class Skills
{
    public enum SkillType
    {
        None = 0, Swords = 1, Jump = 100, Run = 102, All = 999
    }

    public sealed class SkillDef
    {
        public SkillType m_skill;
        public string m_description = string.Empty;
        public UnityEngine.Sprite? m_icon;
        public float m_increseStep = 1f;
    }

    public sealed class Skill
    {
        public SkillDef m_info = new SkillDef();
        public float m_level;
        public float m_accumulator;
    }

    public readonly List<SkillDef> m_skills = new List<SkillDef>();
    private readonly List<Skill> skillData = new List<Skill>();

    public void Awake() { }
    public void Load(object package) { }
    public void Save(object package) { }
    public List<Skill> GetSkillList() => skillData;
}

namespace BenheimQoL.Infrastructure
{
    internal sealed class DiagnosticEvent
    {
        private DiagnosticEvent(string domain, string name) { Domain = domain; Name = name; }
        internal string Domain { get; }
        internal string Name { get; }
        internal readonly Dictionary<string, object?> Fields = new Dictionary<string, object?>();
        internal static DiagnosticEvent Create(string domain, string name) => new DiagnosticEvent(domain, name);
        internal DiagnosticEvent String(string name, string? value) { Fields[name] = value; return this; }
        internal DiagnosticEvent Integer(string name, int value) { Fields[name] = value; return this; }
        internal DiagnosticEvent Number(string name, float value) { Fields[name] = value; return this; }
        internal DiagnosticEvent Boolean(string name, bool value) { Fields[name] = value; return this; }
    }

    internal static class Diagnostics
    {
        internal static readonly List<DiagnosticEvent> Events = new List<DiagnosticEvent>();
        internal static void Emit(DiagnosticEvent value) => Events.Add(value);
    }

    internal static class StableHashFixture
    {
        internal static int GetStableHashCode(this string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value) hash = (hash ^ character) * 16777619;
                return (int)(hash & 0x7fffffff);
            }
        }
    }
}
