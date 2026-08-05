using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BepInEx
{
    internal static class Paths
    {
        internal static string ConfigPath { get; set; } = string.Empty;
    }
}

namespace UnityEngine
{
    internal static class Mathf
    {
        internal static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Min(Math.Max(value, minimum), maximum);
        }
    }

    internal static class Time
    {
        internal static float realtimeSinceStartup { get; set; }
    }

    internal readonly struct Vector3
    {
        internal static Vector3 up => new Vector3();

        public static Vector3 operator +(Vector3 left, Vector3 right)
        {
            return new Vector3();
        }
    }

    internal readonly struct Quaternion
    {
    }

    internal sealed class Transform
    {
        internal Vector3 position { get; } = new Vector3();
        internal Vector3 forward { get; } = new Vector3();
        internal Quaternion rotation { get; } = new Quaternion();
    }
}

internal readonly struct Vector2i : IEquatable<Vector2i>
{
    internal Vector2i(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    internal readonly int x;
    internal readonly int y;
    internal static Vector2i zero => new Vector2i(0, 0);

    public bool Equals(Vector2i other)
    {
        return x == other.x && y == other.y;
    }

    public override bool Equals(object? value)
    {
        return value is Vector2i other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(x, y);
    }
}

internal readonly struct ZDOID : IEquatable<ZDOID>
{
    internal ZDOID(long userId, uint id)
    {
        UserId = userId;
        Id = id;
    }

    internal long UserId { get; }
    internal uint Id { get; }
    internal static ZDOID None => new ZDOID(0L, 0U);

    internal bool IsNone()
    {
        return UserId == 0L && Id == 0U;
    }

    public bool Equals(ZDOID other)
    {
        return UserId == other.UserId && Id == other.Id;
    }

    public override bool Equals(object? value)
    {
        return value is ZDOID other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(UserId, Id);
    }

    public static bool operator ==(ZDOID left, ZDOID right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ZDOID left, ZDOID right)
    {
        return !left.Equals(right);
    }
}

internal sealed class ZPackage
{
    private readonly MemoryStream stream;
    private readonly BinaryReader reader;
    private readonly BinaryWriter writer;

    internal ZPackage()
        : this(Array.Empty<byte>())
    {
        stream.SetLength(0);
    }

    internal ZPackage(byte[] bytes)
    {
        stream = new MemoryStream();
        writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(bytes);
        writer.Flush();
        stream.Position = 0;
        reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
    }

    internal ZPackage(string encoded)
        : this(Convert.FromBase64String(encoded))
    {
    }

    internal void Write(int value) => writer.Write(value);
    internal void Write(long value) => writer.Write(value);
    internal void Write(string value) => writer.Write(value);
    internal void Write(ZDOID value)
    {
        writer.Write(value.UserId);
        writer.Write(value.Id);
    }

    internal void Write(byte[] value)
    {
        writer.Write(value.Length);
        writer.Write(value);
    }

    internal void Write(Vector2i value)
    {
        writer.Write(value.x);
        writer.Write(value.y);
    }

    internal void Write(ZPackage value) => Write(value.GetArray());

    internal int ReadInt() => reader.ReadInt32();
    internal long ReadLong() => reader.ReadInt64();
    internal string ReadString() => reader.ReadString();
    internal ZDOID ReadZDOID() => new ZDOID(reader.ReadInt64(), reader.ReadUInt32());
    internal byte[] ReadByteArray() => reader.ReadBytes(reader.ReadInt32());
    internal Vector2i ReadVector2i() => new Vector2i(reader.ReadInt32(), reader.ReadInt32());
    internal ZPackage ReadPackage() => new ZPackage(ReadByteArray());

    internal byte[] GetArray()
    {
        writer.Flush();
        return stream.ToArray();
    }

    internal string GetBase64() => Convert.ToBase64String(GetArray());
    internal int GetPos() => checked((int)stream.Position);
    internal int Size() => checked((int)stream.Length);
}

internal sealed class ItemDrop
{
    internal sealed class SharedData
    {
        internal string m_name = string.Empty;
        internal int m_maxStackSize;
    }

    internal sealed class ItemData
    {
        internal SharedData m_shared = new SharedData();
        internal object? m_dropPrefab;
        internal int m_stack;
        internal int m_quality;
        internal int m_worldLevel;
        internal Vector2i m_gridPos;

        internal ItemData Clone()
        {
            return new ItemData
            {
                m_shared = new SharedData
                {
                    m_name = m_shared.m_name,
                    m_maxStackSize = m_shared.m_maxStackSize,
                },
                m_dropPrefab = m_dropPrefab,
                m_stack = m_stack,
                m_quality = m_quality,
                m_worldLevel = m_worldLevel,
                m_gridPos = m_gridPos,
            };
        }
    }

    internal static void DropItem(
        ItemData item,
        int amount,
        UnityEngine.Vector3 position,
        UnityEngine.Quaternion rotation)
    {
        throw new InvalidOperationException("Recovery unexpectedly dropped an item.");
    }
}

internal sealed class Inventory
{
    private readonly Dictionary<Vector2i, ItemDrop.ItemData> items = new Dictionary<Vector2i, ItemDrop.ItemData>();

    internal Inventory(string name, object? background, int width, int height)
    {
    }

    internal bool AddItem(ItemDrop.ItemData item, Vector2i position)
    {
        if (items.TryGetValue(position, out ItemDrop.ItemData? existing))
        {
            if (!SameItem(existing, item)
                || existing.m_stack + item.m_stack > existing.m_shared.m_maxStackSize)
            {
                return false;
            }

            existing.m_stack += item.m_stack;
            return true;
        }

        item.m_gridPos = position;
        items.Add(position, item);
        return true;
    }

    internal bool AddItem(ItemDrop.ItemData item)
    {
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                if (AddItem(item, new Vector2i(x, y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal bool RemoveItem(ItemDrop.ItemData item, int amount)
    {
        if (amount <= 0 || amount > item.m_stack || !items.TryGetValue(item.m_gridPos, out ItemDrop.ItemData? stored))
        {
            return false;
        }

        stored.m_stack -= amount;
        if (stored.m_stack == 0)
        {
            items.Remove(stored.m_gridPos);
        }

        return true;
    }

    internal ItemDrop.ItemData GetItemAt(int x, int y)
    {
        return items.TryGetValue(new Vector2i(x, y), out ItemDrop.ItemData? item) ? item : null!;
    }

    internal List<ItemDrop.ItemData> GetAllItems() => items.Values.ToList();

    internal void Save(ZPackage package)
    {
        package.Write(items.Count);
        foreach (ItemDrop.ItemData item in items.Values)
        {
            package.Write(item.m_shared.m_name);
            package.Write(item.m_shared.m_maxStackSize);
            package.Write(item.m_stack);
            package.Write(item.m_quality);
            package.Write(item.m_worldLevel);
            package.Write(item.m_gridPos);
        }
    }

    internal void Load(ZPackage package)
    {
        int count = package.ReadInt();
        for (int index = 0; index < count; index++)
        {
            ItemDrop.ItemData item = new ItemDrop.ItemData
            {
                m_shared = new ItemDrop.SharedData
                {
                    m_name = package.ReadString(),
                    m_maxStackSize = package.ReadInt(),
                },
                m_stack = package.ReadInt(),
                m_quality = package.ReadInt(),
                m_worldLevel = package.ReadInt(),
                m_gridPos = package.ReadVector2i(),
                m_dropPrefab = new object(),
            };
            items.Add(item.m_gridPos, item);
        }
    }

    private static bool SameItem(ItemDrop.ItemData left, ItemDrop.ItemData right)
    {
        return left.m_shared.m_name == right.m_shared.m_name
            && left.m_quality == right.m_quality
            && left.m_worldLevel == right.m_worldLevel;
    }
}

internal sealed class PlayerProfile
{
    internal long PlayerId { get; set; }
    internal long GetPlayerID() => PlayerId;
}

internal sealed class Game
{
    internal static Game instance = null!;
    internal PlayerProfile Profile { get; } = new PlayerProfile();
    internal PlayerProfile GetPlayerProfile() => Profile;
}

internal sealed class ZNet
{
    internal static ZNet instance = new ZNet();
    internal long WorldId { get; set; }
    internal long GetWorldUID() => WorldId;
}

internal sealed class Player
{
    private readonly Inventory inventory;

    internal Player(Inventory inventory)
    {
        this.inventory = inventory;
    }

    internal static Player m_localPlayer = null!;
    internal UnityEngine.Transform transform { get; } = new UnityEngine.Transform();
    internal Inventory GetInventory() => inventory;

    public static implicit operator bool(Player? player) => player != null;
    public static bool operator !(Player? player) => player == null;
}
