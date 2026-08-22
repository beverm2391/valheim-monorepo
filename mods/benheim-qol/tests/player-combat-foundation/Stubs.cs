using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name = string.Empty;
        public static void Destroy(Object value) { }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public sealed class Sprite : Object
    {
    }

    public readonly struct Vector3
    {
        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 operator +(Vector3 left, Vector3 right) =>
            new Vector3(left.x + right.x, left.y + right.y, left.z + right.z);
        public static Vector3 operator *(Vector3 value, float scale) =>
            new Vector3(value.x * scale, value.y * scale, value.z * scale);

        public readonly float x;
        public readonly float y;
        public readonly float z;
    }

    public readonly struct Quaternion
    {
        public static Quaternion identity => new Quaternion();
    }

    public sealed class Transform
    {
        public Vector3 position;
    }

    public sealed class GameObject : Object
    {
        private readonly Dictionary<Type, object> components = new Dictionary<Type, object>();

        public void AddComponent<T>(T component) where T : class
        {
            components[typeof(T)] = component;
        }

        public T? GetComponent<T>() where T : class
        {
            return components.TryGetValue(typeof(T), out object? value) ? (T)value : null;
        }
    }

    public static class Mathf
    {
        public static float Max(float left, float right) => Math.Max(left, right);
        public static float Min(float left, float right) => Math.Min(left, right);
    }
}

public sealed class ZNet
{
    public static ZNet? instance { get; set; }
    public double TimeSeconds { get; set; }
    public double GetTimeSeconds() => TimeSeconds;
}

public readonly struct ZDOID
{
    public ZDOID(long id)
    {
        Id = id;
    }

    public long Id { get; }
}

public static class Skills
{
    public enum SkillType
    {
        None,
        All
    }
}

public sealed class HitData
{
    public enum DamageType
    {
        Blunt,
        Slash,
        Pierce,
        Fire,
        Frost,
        Lightning,
        Poison,
        Spirit
    }

    public enum DamageModifier
    {
        Normal,
        Resistant,
        SlightlyResistant
    }

    public struct DamageModPair
    {
        public DamageType m_type;
        public DamageModifier m_modifier;
    }

    public struct DamageTypes
    {
        public float m_damage;
        public float m_blunt;
        public float m_slash;
        public float m_pierce;
        public float m_chop;
        public float m_pickaxe;
        public float m_fire;
        public float m_frost;
        public float m_lightning;
        public float m_poison;
        public float m_spirit;

        public void Modify(float multiplier)
        {
            m_damage *= multiplier;
            m_blunt *= multiplier;
            m_slash *= multiplier;
            m_pierce *= multiplier;
            m_chop *= multiplier;
            m_pickaxe *= multiplier;
            m_fire *= multiplier;
            m_frost *= multiplier;
            m_lightning *= multiplier;
            m_poison *= multiplier;
            m_spirit *= multiplier;
        }

        public float GetTotalDamage() =>
            m_damage + m_blunt + m_slash + m_pierce + m_chop + m_pickaxe
            + m_fire + m_frost + m_lightning + m_poison + m_spirit;
    }

    public struct DamageModifiers
    {
        public DamageModifier m_blunt;
        public DamageModifier m_slash;
        public DamageModifier m_pierce;

        public void Apply(List<DamageModPair> modifiers)
        {
            foreach (DamageModPair modifier in modifiers)
            {
                switch (modifier.m_type)
                {
                    case DamageType.Blunt:
                        ApplyIfBetter(ref m_blunt, modifier.m_modifier);
                        break;
                    case DamageType.Slash:
                        ApplyIfBetter(ref m_slash, modifier.m_modifier);
                        break;
                    case DamageType.Pierce:
                        ApplyIfBetter(ref m_pierce, modifier.m_modifier);
                        break;
                }
            }
        }

        private static void ApplyIfBetter(
            ref DamageModifier current,
            DamageModifier configured)
        {
            if (current == DamageModifier.Normal
                || (current == DamageModifier.SlightlyResistant
                    && configured == DamageModifier.Resistant))
            {
                current = configured;
            }
        }
    }

    public DamageTypes m_damage;
}

public class Character
{
}

public class StatusEffect : UnityEngine.ScriptableObject
{
    public string m_name = string.Empty;
    public string m_category = string.Empty;
    public UnityEngine.Sprite? m_icon;
    public string m_tooltip = string.Empty;
    public float m_ttl;
    public string m_startMessage = string.Empty;
    public string m_stopMessage = string.Empty;
    public Character? m_character;
    protected float m_time;

    public int NameHash() => name.GetHashCode(StringComparison.Ordinal);

    public virtual StatusEffect Clone() => (StatusEffect)MemberwiseClone();

    public virtual void Setup(Character character)
    {
        m_character = character;
    }

    public virtual void Stop() { }

    public virtual void UpdateStatusEffect(float dt)
    {
        m_time += dt;
    }

    public virtual bool IsDone() => m_ttl > 0f && m_time > m_ttl;

    public virtual void ResetTime()
    {
        m_time = 0f;
    }

    public virtual void ModifyAttack(Skills.SkillType skill, ref HitData hitData) { }
    public virtual void ModifyStaminaRegen(ref float staminaRegen) { }
    public virtual void ModifyDamageMods(ref HitData.DamageModifiers modifiers) { }
}

public class SE_Stats : StatusEffect
{
    public float m_tickInterval;
    public float m_healthPerTickMinHealthPercentage;
    public float m_healthPerTick;
    public Skills.SkillType m_modifyAttackSkill;
    public float m_damageModifier = 1f;
    public float m_staminaRegenMultiplier = 1f;
    public List<HitData.DamageModPair> m_mods = new List<HitData.DamageModPair>();

    private float tickTimer;

    public override void UpdateStatusEffect(float dt)
    {
        base.UpdateStatusEffect(dt);
        if (m_tickInterval <= 0f)
        {
            return;
        }

        tickTimer += dt;
        if (tickTimer >= m_tickInterval)
        {
            tickTimer = 0f;
            if (m_character is Player player
                && player.GetHealth() / player.GetMaxHealth()
                    >= m_healthPerTickMinHealthPercentage)
            {
                player.Heal(m_healthPerTick);
            }
        }
    }

    public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
    {
        if (skill == m_modifyAttackSkill || m_modifyAttackSkill == Skills.SkillType.All)
        {
            hitData.m_damage.Modify(m_damageModifier);
        }
    }

    public override void ModifyStaminaRegen(ref float staminaRegen)
    {
        if (m_staminaRegenMultiplier > 1f)
        {
            staminaRegen += m_staminaRegenMultiplier - 1f;
        }
        else
        {
            staminaRegen *= m_staminaRegenMultiplier;
        }
    }

    public override void ModifyDamageMods(ref HitData.DamageModifiers modifiers)
    {
        modifiers.Apply(m_mods);
    }
}

public sealed class EffectList
{
    public bool Available { get; set; }
    public int CreateCount { get; private set; }

    public bool HasEffects() => Available;

    public UnityEngine.GameObject[] Create(
        UnityEngine.Vector3 position,
        UnityEngine.Quaternion rotation)
    {
        CreateCount++;
        return Array.Empty<UnityEngine.GameObject>();
    }
}

public sealed class ItemDrop
{
    public ItemData m_itemData = new ItemData();

    public sealed class ItemData
    {
        public SharedData m_shared = new SharedData();
    }

    public sealed class SharedData
    {
        public StatusEffect? m_consumeStatusEffect;
        public StatusEffect? m_fullAdrenalineSE;
    }
}

public sealed class SEMan
{
    private readonly Player owner;
    private readonly Dictionary<int, StatusEffect> active =
        new Dictionary<int, StatusEffect>();

    internal SEMan(Player owner)
    {
        this.owner = owner;
    }

    public StatusEffect? AddStatusEffect(int hash, bool resetTime = false)
    {
        if (active.TryGetValue(hash, out StatusEffect? current))
        {
            if (resetTime)
            {
                current.ResetTime();
            }

            return null;
        }

        StatusEffect? definition = ObjectDB.instance?.GetStatusEffect(hash);
        if (definition == null)
        {
            return null;
        }

        StatusEffect clone = definition.Clone();
        active.Add(hash, clone);
        clone.Setup(owner);
        return clone;
    }

    public bool HaveStatusEffect(int hash) => active.ContainsKey(hash);

    public StatusEffect? GetStatusEffect(int hash) =>
        active.TryGetValue(hash, out StatusEffect? value) ? value : null;

    public void GetHUDStatusEffects(List<StatusEffect> effects)
    {
        foreach (StatusEffect effect in active.Values)
        {
            if (effect.m_icon != null)
            {
                effects.Add(effect);
            }
        }
    }

    public bool RemoveStatusEffect(int hash, bool quiet = false)
    {
        if (!active.TryGetValue(hash, out StatusEffect? effect))
        {
            return false;
        }

        active.Remove(hash);
        effect.Stop();
        return true;
    }

    internal void Tick(float dt)
    {
        List<int> expired = new List<int>();
        foreach ((int hash, StatusEffect effect) in active)
        {
            effect.UpdateStatusEffect(dt);
            if (effect.IsDone())
            {
                expired.Add(hash);
            }
        }

        foreach (int hash in expired)
        {
            RemoveStatusEffect(hash);
        }
    }

    internal int Count => active.Count;
}

public sealed class ObjectDB
{
    private readonly Dictionary<string, UnityEngine.GameObject> items =
        new Dictionary<string, UnityEngine.GameObject>();

    public ObjectDB()
    {
        instance = this;
    }

    public static ObjectDB? instance { get; private set; }
    public List<StatusEffect> m_StatusEffects { get; } = new List<StatusEffect>();

    public StatusEffect? GetStatusEffect(int hash)
    {
        return m_StatusEffects.Find(effect => effect.NameHash() == hash);
    }

    public UnityEngine.GameObject? GetItemPrefab(string name)
    {
        return items.TryGetValue(name, out UnityEngine.GameObject? value) ? value : null;
    }

    internal void AddItem(string name, ItemDrop item)
    {
        UnityEngine.GameObject prefab = new UnityEngine.GameObject { name = name };
        prefab.AddComponent(item);
        items.Add(name, prefab);
    }
}

public sealed class Player : Character
{
    public Player(float health, float maximumHealth)
    {
        Health = health;
        MaximumHealth = maximumHealth;
        statusEffects = new SEMan(this);
    }

    public static Player? m_localPlayer;
    public float Health { get; set; }
    public float MaximumHealth { get; }
    public float Adrenaline { get; set; }
    public float MaximumAdrenaline { get; set; }
    public UnityEngine.Transform transform { get; } = new UnityEngine.Transform();
    public EffectList m_adrenalinePopEffects = new EffectList();

    private readonly SEMan statusEffects;

    public float GetHealth() => Health;
    public float GetMaxHealth() => MaximumHealth;
    public float GetHealthPercentage() => MaximumHealth > 0f ? Health / MaximumHealth : 0f;
    public float GetAdrenaline() => Adrenaline;
    public float GetMaxAdrenaline() => MaximumAdrenaline;
    public SEMan GetSEMan() => statusEffects;
    public void Heal(float amount) => Health = Math.Min(MaximumHealth, Health + amount);
}

namespace BenheimQoL.Infrastructure
{
    internal sealed class DiagnosticEvent
    {
        private DiagnosticEvent(string domain, string name)
        {
            Domain = domain;
            Name = name;
        }

        internal string Domain { get; }
        internal string Name { get; }
        internal Dictionary<string, object?> Fields { get; } =
            new Dictionary<string, object?>();

        internal static DiagnosticEvent Create(string domain, string name) =>
            new DiagnosticEvent(domain, name);

        internal DiagnosticEvent String(string key, string? value)
        {
            Fields[key] = value;
            return this;
        }

        internal DiagnosticEvent Integer(string key, long value)
        {
            Fields[key] = value;
            return this;
        }

        internal DiagnosticEvent Number(string key, float value)
        {
            Fields[key] = value;
            return this;
        }

        internal DiagnosticEvent Boolean(string key, bool value)
        {
            Fields[key] = value;
            return this;
        }
    }

    internal static class Diagnostics
    {
        internal static List<DiagnosticEvent> Emitted { get; } =
            new List<DiagnosticEvent>();

        internal static void Event(string feature, string action, string details = "") { }
        internal static void Emit(DiagnosticEvent diagnosticEvent) => Emitted.Add(diagnosticEvent);
        internal static void Reset() => Emitted.Clear();
    }

    internal static class WorldFeedback
    {
        internal static List<string> Messages { get; } = new List<string>();

        internal static void ShowAbovePlayer(Player player, string text)
        {
            Messages.Add(text);
        }

        internal static void Reset()
        {
            Messages.Clear();
        }
    }
}

namespace BenheimQoL.PlayerCombat
{
    internal static class PlayerCombatRuntime
    {
        internal static int StoppedEffects { get; private set; }
        internal static int ExpiredEffects { get; private set; }
        internal static EarnedStatePresentation? Presentation { get; set; }

        internal static void ObserveEffectStopped(
            Player player,
            EarnedCombatState state,
            int tier,
            bool expired)
        {
            StoppedEffects++;
            if (expired)
            {
                ExpiredEffects++;
            }
        }

        internal static void ResetStops()
        {
            StoppedEffects = 0;
            ExpiredEffects = 0;
        }

        internal static void CompletePerfectDefensePresentation(
            Player player,
            string? adrenalineLine,
            bool nativeCharmActivated = false)
        {
            Presentation?.CompletePerfectDefense(
                player,
                adrenalineLine,
                nativeCharmActivated);
        }
    }
}
