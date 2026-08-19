using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

public class InventoryGui
{
    public static InventoryGui m_instance = new InventoryGui();
    public static InventoryGui instance => m_instance;
    public float m_craftBonusChance;
    public int m_craftBonusAmount;
}

public class CookingStation
{
}

public class SE_Rested
{
}

public class CraftingStation
{
    public Skills.SkillType m_craftingSkill;
}

public static class Skills
{
    public enum SkillType
    {
        None,
        Cooking,
        Other
    }
}

public class Player
{
    private readonly CraftingStation? station;

    public Player(CraftingStation? station)
    {
        this.station = station;
    }

    public static Player? m_localPlayer;

    public CraftingStation? GetCurrentCraftingStation()
    {
        return station;
    }
}

namespace UnityEngine
{
    public static class Random
    {
        public static float value => 0f;
    }
}

namespace BenheimQoL.Infrastructure
{
    internal sealed class DiagnosticEvent
    {
        private readonly Dictionary<string, int> integers = new Dictionary<string, int>();

        internal static DiagnosticEvent Create(string domain, string name) => new DiagnosticEvent();
        internal DiagnosticEvent String(string name, string? value) => this;
        internal DiagnosticEvent Integer(string name, int value)
        {
            integers[name] = value;
            return this;
        }
        internal DiagnosticEvent Number(string name, float value) => this;
        internal DiagnosticEvent Boolean(string name, bool value) => this;
        internal int IntegerValue(string name) => integers[name];
    }

    internal static class Diagnostics
    {
        internal static int Emitted { get; private set; }
        internal static DiagnosticEvent? Last { get; private set; }

        internal static void Emit(DiagnosticEvent diagnosticEvent)
        {
            Last = diagnosticEvent;
            Emitted++;
        }
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute
    {
        internal HarmonyPatch(Type type, string methodName)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyTranspiler : Attribute
    {
    }

    internal readonly record struct ExceptionBlock(int Id);

    internal sealed class CodeInstruction
    {
        internal CodeInstruction(OpCode opcode, object? operand = null)
        {
            this.opcode = opcode;
            this.operand = operand;
        }

        internal OpCode opcode;
        internal object? operand;
        internal List<Label> labels = new List<Label>();
        internal List<ExceptionBlock> blocks = new List<ExceptionBlock>();
    }

    internal static class AccessTools
    {
        internal static MethodInfo? Method(Type type, string name)
        {
            return type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        }

        internal static FieldInfo? Field(Type type, string name)
        {
            return type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        }
    }
}
