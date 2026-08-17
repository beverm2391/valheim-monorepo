using System;

namespace UnityEngine
{
    public readonly struct Vector3
    {
        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public readonly float x;
        public readonly float y;
        public readonly float z;
    }
}

public readonly struct ZDOID
{
    public ZDOID(long id)
    {
        Id = id;
    }

    public long Id { get; }
}

public class Character
{
}

public class StatusEffect
{
    public string name = string.Empty;
    public Character? m_character;

    public int NameHash() => name.GetHashCode(StringComparison.Ordinal);
    public virtual void Stop() { }
}

public class SE_Stats : StatusEffect
{
}

public sealed class SEMan
{
    private readonly System.Collections.Generic.Dictionary<int, StatusEffect> active =
        new System.Collections.Generic.Dictionary<int, StatusEffect>();

    public StatusEffect? AddStatusEffect(int hash, bool resetTime = false)
    {
        if (active.ContainsKey(hash))
        {
            return null;
        }

        StatusEffect? definition = ObjectDB.instance?.GetStatusEffect(hash);
        if (definition == null)
        {
            return null;
        }

        active.Add(hash, definition);
        return definition;
    }

    public bool HaveStatusEffect(int hash) => active.ContainsKey(hash);

    public bool RemoveStatusEffect(int hash, bool quiet = false)
    {
        return active.Remove(hash);
    }
}

public sealed class ObjectDB
{
    public ObjectDB()
    {
        instance = this;
    }

    public static ObjectDB? instance { get; private set; }
    public System.Collections.Generic.List<StatusEffect> m_StatusEffects { get; } =
        new System.Collections.Generic.List<StatusEffect>();

    public StatusEffect? GetStatusEffect(int hash)
    {
        return m_StatusEffects.Find(effect => effect.NameHash() == hash);
    }
}

public sealed class MessageHud
{
    public static MessageHud? instance;
    public string? LastBanner { get; private set; }

    public void ShowBiomeFoundMsg(string message, bool playStinger)
    {
        LastBanner = message;
    }
}

public sealed class Player : Character
{
    public Player(float health, float maximumHealth)
    {
        Health = health;
        MaximumHealth = maximumHealth;
    }

    public float Health { get; set; }
    public float MaximumHealth { get; }
    private readonly SEMan statusEffects = new SEMan();

    public float GetHealth() => Health;
    public float GetMaxHealth() => MaximumHealth;
    public SEMan GetSEMan() => statusEffects;
}

namespace BenheimQoL.Infrastructure
{
    internal static class Diagnostics
    {
        internal static void Event(string feature, string action, string details = "") { }
    }
}

namespace BenheimQoL.PlayerCombat
{
    internal static class PlayerCombatRuntime
    {
        internal static int StoppedEffects { get; private set; }

        internal static void ObserveEffectStopped(
            Player player,
            EarnedCombatState state,
            int tier)
        {
            StoppedEffects++;
        }
    }
}
