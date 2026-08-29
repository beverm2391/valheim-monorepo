using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

public sealed class TreeLog
{
    public TreeLog(string prefabName)
    {
        gameObject = new UnityEngine.GameObject(prefabName);
    }

    public UnityEngine.GameObject gameObject;

    private void Destroy(HitData hitData)
    {
    }
}

public sealed class HitData
{
}

public sealed class Game
{
    public UnityEngine.GameObject CheckDropConversion(
        HitData hitData,
        ItemDrop itemDrop,
        UnityEngine.GameObject dropPrefab,
        ref int dropCount) => dropPrefab;
}

public sealed class ItemDrop
{
}

public sealed class ObjectDB
{
    private readonly Dictionary<string, UnityEngine.GameObject> prefabs =
        new Dictionary<string, UnityEngine.GameObject>(StringComparer.Ordinal);

    public static ObjectDB? instance;

    public UnityEngine.GameObject? GetItemPrefab(string name)
    {
        return prefabs.TryGetValue(name, out UnityEngine.GameObject? prefab)
            ? prefab
            : null;
    }

    public void Add(UnityEngine.GameObject prefab)
    {
        prefabs[prefab.name] = prefab;
    }
}

public static class Utils
{
    public static string GetPrefabName(UnityEngine.GameObject gameObject) => gameObject.name;
}

namespace UnityEngine
{
    public sealed class GameObject
    {
        public GameObject(string name)
        {
            this.name = name;
        }

        public string name;
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute
    {
        internal HarmonyPatch(Type type, string methodName, Type[] argumentTypes)
        {
        }
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

        internal bool Calls(MethodInfo method)
        {
            return (opcode == OpCodes.Call || opcode == OpCodes.Callvirt)
                && Equals(operand, method);
        }
    }

    internal static class AccessTools
    {
        internal static MethodInfo? Method(Type type, string name, Type[] argumentTypes)
        {
            return type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                binder: null,
                types: argumentTypes,
                modifiers: null);
        }
    }
}
