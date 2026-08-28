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
    public float Stamina { get; set; }
    public float ResolvedBuildStamina { get; set; }
    public float LastStaminaCheck { get; private set; }

    public CraftingStation? GetCurrentCraftingStation()
    {
        return station;
    }

    public bool HaveStamina(float amount)
    {
        LastStaminaCheck = amount;
        return Stamina >= amount;
    }

    public bool TryPlacePiece(Piece piece) => true;

    private float GetBuildStamina() => ResolvedBuildStamina;
}

public class PieceTable
{
    private readonly Piece selectedPiece;

    public PieceTable(Piece selectedPiece)
    {
        this.selectedPiece = selectedPiece;
    }

    public Piece GetSelectedPiece() => selectedPiece;
}

public class Piece
{
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject("Piece");

    public T? GetComponent<T>() where T : class => gameObject.GetComponent<T>();

    public static implicit operator bool(Piece? piece) => piece is not null;
}

public class Plant
{
    public static implicit operator bool(Plant? plant) => plant is not null;
}

public class Humanoid
{
}

public class Pickable
{
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject("Pickable");
    public UnityEngine.GameObject? m_itemPrefab;
    public bool m_tarPreventsPicking;

    public bool Interact(Humanoid user, bool hold, bool alt) => false;

    public T? GetComponent<T>() where T : class
    {
        return gameObject.GetComponent<T>();
    }
}

public class Floating
{
    public bool InTar { get; set; }
    public bool IsInTar() => InTar;
}

public class ItemDrop
{
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject("Item");
    public ItemData m_itemData = new ItemData();
    public bool TarState { get; set; }

    public bool Interact(Humanoid user, bool hold, bool alt) => false;
    public bool InTar() => TarState;

    public sealed class ItemData
    {
        public UnityEngine.GameObject? m_dropPrefab;
        public SharedData m_shared = new SharedData();

        public enum ItemType
        {
            None,
            Material
        }

        public sealed class SharedData
        {
            public string m_name = string.Empty;
            public ItemType m_itemType;
        }
    }
}

public static class Utils
{
    public static string GetPrefabName(UnityEngine.GameObject gameObject)
    {
        return gameObject.name;
    }
}

namespace UnityEngine
{
    public sealed class GameObject
    {
        private readonly Dictionary<Type, object> components = new Dictionary<Type, object>();

        public GameObject(string name)
        {
            this.name = name;
        }

        public string name;

        public void AddComponent<T>(T component) where T : class
        {
            components[typeof(T)] = component;
        }

        public T? GetComponent<T>() where T : class
        {
            return components.TryGetValue(typeof(T), out object? component)
                ? (T)component
                : null;
        }
    }

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

namespace BenheimQoL.Farming
{
    internal static class FarmingReflection
    {
        internal static float GetBuildStamina(Player player) => player.ResolvedBuildStamina;
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute
    {
        internal HarmonyPatch()
        {
        }

        internal HarmonyPatch(Type type, string methodName)
        {
        }

        internal HarmonyPatch(Type type, string methodName, Type[] argumentTypes)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyTranspiler : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPostfix : Attribute
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

        internal void MoveLabelsTo(CodeInstruction target)
        {
            target.labels.AddRange(labels);
            labels.Clear();
        }

        internal void MoveBlocksTo(CodeInstruction target)
        {
            target.blocks.AddRange(blocks);
            blocks.Clear();
        }
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
