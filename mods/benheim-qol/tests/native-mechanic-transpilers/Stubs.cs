using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

public class InventoryGui
{
    public float m_craftBonusChance;
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
