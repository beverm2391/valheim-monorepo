using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using BenheimQoL.Woodcutting;

// This fixture models the inspected native lifecycle, not a Unity/network runtime:
// owner RPC delivery can reset the ZDO before deferred Unity destruction. Production
// Cleave and postfixes are compiled unchanged; postfix dispatch is explicit here.
namespace UnityEngine
{
    internal class Object
    {
        public static implicit operator bool(Object? value) => value != null;
    }

    internal class Component : Object
    {
        internal ZNetView View = new();
        internal T GetComponent<T>() where T : class => (View as T)!;
    }

    internal readonly record struct Vector3(float x, float y, float z)
    {
        internal static Vector3 up => new(0, 1, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x * b, a.y * b, a.z * b);
    }

    internal static class Mathf
    {
        internal static float InverseLerp(float a, float b, float value) => Math.Clamp((value - a) / (b - a), 0, 1);
        internal static float Lerp(float a, float b, float value) => a + (b - a) * value;
    }

    internal static class Random
    {
        internal static float value;
    }
}

internal sealed class ZDO
{
    internal long Owner = 1;
    internal bool Valid = true;
}

internal sealed class ZNetView : UnityEngine.Object
{
    internal ZDO? Data = new();
    internal readonly List<(long Owner, HitData Hit)> Calls = new();
    internal Action? OnDamage;
    internal bool IsValid() => Data?.Valid == true;
    internal void ResetZDO() => Data = null;

    internal void InvokeRPC(string method, HitData hit)
    {
        // Like native InvokeRPC, owner lookup dereferences the current ZDO without
        // checking validity. ResetZDO must reproduce the original exception here.
        Calls.Add((Data!.Owner, hit));
        OnDamage?.Invoke();
    }
}

internal sealed class TreeBase : UnityEngine.Component
{
    internal void Damage(HitData hit)
    {
        View.InvokeRPC("RPC_Damage", hit);
        PostfixDispatch.Invoke("StandingTreeDamagePatch", this, hit);
    }
}

internal sealed class TreeLog : UnityEngine.Component
{
    internal void Damage(HitData hit)
    {
        if (View.IsValid())
        {
            View.InvokeRPC("RPC_Damage", hit);
        }
        PostfixDispatch.Invoke("FallenLogDamagePatch", this, hit);
    }
}

internal static class PostfixDispatch
{
    internal static void Invoke(string name, UnityEngine.Component target, HitData hit)
    {
        MethodInfo postfix = typeof(WoodcuttingPatches)
            .GetNestedType(name, BindingFlags.NonPublic)!
            .GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            postfix.Invoke(null, new object[] { target, hit });
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }
}

internal class Character : UnityEngine.Object { }
internal sealed class Player : Character
{
    internal static Player m_localPlayer = new();
    internal float SkillFactor = 0.25f;
    internal long GetZDOID() => 1;
    internal float GetSkillFactor(Skills.SkillType type) => SkillFactor;
}
internal static class Skills
{
    internal enum SkillType { WoodCutting }
}
internal sealed class HitData
{
    internal struct DamageTypes
    {
        internal float m_chop, m_slash;
        internal void Modify(float scale) { m_chop *= scale; m_slash *= scale; }
    }
    internal DamageTypes m_damage;
    internal float m_pushForce, m_radius, m_skillRaiseAmount;
    internal long m_attacker;
    internal UnityEngine.Vector3 m_point;
    internal Character GetAttacker() => Player.m_localPlayer;
    internal HitData Clone() => (HitData)MemberwiseClone();
}
internal sealed class DamageText
{
    internal enum TextType { Bonus }
    internal static DamageText instance = new();
    internal int Shown;
    internal void ShowText(TextType type, UnityEngine.Vector3 point, string text, bool player) => Shown++;
}
namespace BenheimQoL.CombatFeedback
{
    internal enum CombatFeedbackTrigger { Cleave }
    internal static class CombatFeedbackController
    {
        internal static int Shakes;
        internal static void RequestShake(CombatFeedbackTrigger trigger) => Shakes++;
    }
}
namespace BenheimQoL.Infrastructure
{
    internal static class Diagnostics
    {
        internal static readonly List<string> Events = new();
        internal static string Bool(bool value) => value ? "true" : "false";
        internal static void Event(string domain, string action, string details) => Events.Add($"{action} {details}");
    }
}
