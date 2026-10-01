using System;
using System.Collections.Generic;
using BenheimInventoryProtocol;

namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed { get; set; }

        public static bool operator !(Object? value) => value is null || value.Destroyed;
        public static implicit operator bool(Object? value) => value is not null && !value.Destroyed;

        public static bool operator ==(Object? left, Object? right)
        {
            bool leftMissing = left is null || left.Destroyed;
            bool rightMissing = right is null || right.Destroyed;
            return leftMissing ? rightMissing : !rightMissing && ReferenceEquals(left, right);
        }

        public static bool operator !=(Object? left, Object? right) => !(left == right);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => base.GetHashCode();
    }

    public sealed class Transform
    {
        public Vector3 position;
    }

    public struct Vector3
    {
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float x;
        public float y;
        public float z;
        public static Vector3 operator -(Vector3 left, Vector3 right) =>
            new Vector3(left.x - right.x, left.y - right.y, left.z - right.z);
    }

    public struct Vector2
    {
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float x;
        public float y;
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
    }

    public struct Quaternion
    {
        public static Quaternion identity => new Quaternion();
    }

    public static class Mathf
    {
        public const float Rad2Deg = 57.29578f;
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static int RoundToInt(float value) => (int)Math.Round(value);
        public static int Max(int left, int right) => Math.Max(left, right);
    }

    public static class Time
    {
        public static float unscaledTime { get; set; }
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(params object[] arguments) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPostfix : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPrefix : Attribute { }
}

public sealed class GameObject
{
    public string name = string.Empty;
}

public sealed class Player : UnityEngine.Object
{
    public static Player? m_localPlayer;
    private readonly Inventory inventory;
    public readonly UnityEngine.Transform transform = new UnityEngine.Transform();

    public Player(Inventory inventory) => this.inventory = inventory;
    public Inventory GetInventory() => inventory;
}

public sealed class Container : UnityEngine.Object
{
    private static int nextInstanceId;
    private readonly int instanceId = ++nextInstanceId;
    public readonly GameObject gameObject = new GameObject();
    public readonly UnityEngine.Transform transform = new UnityEngine.Transform();
    public Inventory? m_inventory;

    public Container(string name, Inventory? inventory)
    {
        gameObject.name = name;
        m_inventory = inventory;
    }

    public Inventory? GetInventory() => m_inventory;
    public string GetHoverName() => gameObject.name;
    public int GetInstanceID() => instanceId;
}

public sealed class Inventory
{
    private readonly List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>();
    public int GridReadCount { get; private set; }
    public int ContentsReadCount { get; private set; }
    public int? ThrowOnGridRead { get; set; }
    public int? ThrowOnContentsRead { get; set; }
    public bool ThrowOnContainsItemByName { get; set; }
    public bool HasRoom { get; set; } = true;

    public void Add(ItemDrop.ItemData item) => items.Add(item);

    public IEnumerable<ItemDrop.ItemData> GetAllItemsInGridOrder()
    {
        GridReadCount++;
        if (ThrowOnGridRead == GridReadCount)
        {
            throw new InvalidOperationException("Injected player inventory scan failure.");
        }

        return items;
    }

    public IEnumerable<ItemDrop.ItemData> GetAllItems()
    {
        ContentsReadCount++;
        if (ThrowOnContentsRead == ContentsReadCount)
        {
            throw new InvalidOperationException("Injected chest contents scan failure.");
        }

        return items;
    }

    public bool ContainsItemByName(string name)
    {
        if (ThrowOnContainsItemByName)
        {
            throw new InvalidOperationException("Injected item-name scan failure.");
        }

        foreach (ItemDrop.ItemData item in items)
        {
            if (item.m_stack > 0 && item.m_shared.m_name == name)
            {
                return true;
            }
        }

        return false;
    }

    public bool CanAddItem(ItemDrop.ItemData item, int amount) => HasRoom && amount > 0;
}

public static class ItemDrop
{
    public sealed class SharedData
    {
        public string m_name;
        public SharedData(string name) => m_name = name;
    }

    public sealed class ItemData
    {
        public int m_stack;
        public SharedData m_shared;
        public ItemData(string name, int stack = 1)
        {
            m_shared = new SharedData(name);
            m_stack = stack;
        }
    }
}

public sealed class MoveItemEffects
{
    public void Create(UnityEngine.Vector3 position, UnityEngine.Quaternion rotation) { }
}

public sealed class InventoryGui : UnityEngine.Object
{
    public readonly MoveItemEffects m_moveItemEffects = new MoveItemEffects();
    public readonly UnityEngine.Transform transform = new UnityEngine.Transform();
}

public static class InventoryVisibility
{
    public static bool IsOpen(InventoryGui gui) => false;
}

public static class NearbyContainerIndex
{
    public static List<Container> Containers = new List<Container>();
    public static List<Container> FindAccessibleContainers(Player player, float radius, Container? current) => Containers;
}

public static class PocketItems
{
    public static bool IsPocketed(Player player, ItemDrop.ItemData item) => false;
}

public static class TopLeftFeedbackHud
{
    public static readonly List<string> Messages = new List<string>();
    public static void ShowTransient(string message) => Messages.Add(message);
}

public sealed class Localization
{
    public static Localization? instance;
    public string Localize(string value) => value.TrimStart('$');
}

public static class MessageHud
{
    public enum MessageType { TopLeft, Center, Show }
}

public sealed class ZRpc
{
    private Action<ZRpc, string, string, string>? resultHandler;
    public readonly List<RpcCall> Calls = new List<RpcCall>();
    public bool Connected { get; set; } = true;

    public bool IsConnected() => Connected;

    public void Register<T1, T2, T3>(string name, Action<ZRpc, T1, T2, T3> handler)
    {
        if (name == BenheimQoL.InventoryFeature.PutAwayLeaseProtocol.ResultRpc)
        {
            resultHandler = (rpc, first, second, third) => handler(
                rpc,
                (T1)(object)first,
                (T2)(object)second,
                (T3)(object)third);
        }
    }

    public void Invoke(string name, params object[] arguments)
    {
        Calls.Add(new RpcCall(name, arguments));
        HarnessEvents.Events.Add($"rpc:{name}");
    }

    public void Respond(string operationId, string outcome, string reason)
    {
        if (resultHandler == null)
        {
            throw new InvalidOperationException("The Put Away result RPC was not registered.");
        }

        resultHandler(this, operationId, outcome, reason);
    }
}

public sealed class RpcCall
{
    public RpcCall(string name, object[] arguments) { Name = name; Arguments = arguments; }
    public string Name { get; }
    public object[] Arguments { get; }
    public string? OperationId => Arguments.Length > 0 ? Arguments[0] as string : null;
}

public sealed class ZNet
{
    public static ZNet? instance;
    private readonly ZRpc rpc;
    public ZNet(ZRpc rpc) => this.rpc = rpc;
    public ZRpc GetServerRPC() => rpc;
    public void Shutdown() { }
}

namespace BenheimQoL.Infrastructure
{
    internal sealed class DiagnosticEvent
    {
        private readonly Dictionary<string, object?> fields = new Dictionary<string, object?>();
        private DiagnosticEvent(string domain, string name) { Domain = domain; Name = name; }
        internal string Domain { get; }
        internal string Name { get; }
        internal static DiagnosticEvent Create(string domain, string name) => new DiagnosticEvent(domain, name);
        internal DiagnosticEvent String(string name, string? value) { fields[name] = value; return this; }
        internal DiagnosticEvent Integer(string name, int value) { fields[name] = value; return this; }
        internal DiagnosticEvent Boolean(string name, bool value) { fields[name] = value; return this; }
    }

    internal static class Diagnostics
    {
        private static int nextOperation;
        internal static readonly List<string> Events = new List<string>();
        internal static string? ThrowOnEmitName { get; set; }

        internal static string NewOperationId() => $"operation-{++nextOperation}";
        internal static string Bool(bool value) => value ? "true" : "false";
        internal static void Event(string feature, string action, string details = "") { }

        internal static void Emit(DiagnosticEvent diagnosticEvent)
        {
            if (ThrowOnEmitName == diagnosticEvent.Name)
            {
                throw new InvalidOperationException("Injected diagnostic sink failure.");
            }

            Events.Add(diagnosticEvent.Name);
        }

        internal static void ResetForTest()
        {
            nextOperation = 0;
            Events.Clear();
            ThrowOnEmitName = null;
        }
    }
}

namespace BenheimQoL.InventoryFeature
{
    internal static class QuickStackFeedback
    {
        internal static readonly List<string> Messages = new List<string>();
        internal static void ShowDetailedResult(Player player, bool inventoryWasOpen, string message) => Messages.Add(message);
        internal static void ShowAbovePlayerSummaryIfInventoryWasClosed(Player player, bool inventoryWasOpen, int movedItems) { }
    }

    internal static class QuickStackDiagnostics
    {
        internal static void ItemMoved(string operationId, ItemDrop.ItemData item, int amount, Container container, string location) { }
    }

    internal sealed class InventoryTransactionDiagnosticSink : IInventoryTransactionDiagnosticSink
    {
        internal static readonly InventoryTransactionDiagnosticSink Instance = new InventoryTransactionDiagnosticSink();
        public void Emit(InventoryTransactionDiagnosticEvent diagnosticEvent) { }
    }
}

public static class HarnessEvents
{
    public static readonly List<string> Events = new List<string>();
}

public static class Plugin
{
    public const string PluginVersion = "test";
    public static readonly TestLog Log = new TestLog();
}

public sealed class TestLog
{
    public readonly List<string> Errors = new List<string>();
    public void LogError(string message) => Errors.Add(message);
}
