public readonly struct ZDOID : IEquatable<ZDOID>
{
    public static readonly ZDOID None = new ZDOID(0L, 0u);

    public ZDOID(long userId, uint id)
    {
        UserId = userId;
        Id = id;
    }

    public long UserId { get; }
    public uint Id { get; }

    public bool IsNone() => UserId == 0L && Id == 0u;
    public bool Equals(ZDOID other) => UserId == other.UserId && Id == other.Id;
    public override bool Equals(object? value) => value is ZDOID other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(UserId, Id);
    public static bool operator ==(ZDOID left, ZDOID right) => left.Equals(right);
    public static bool operator !=(ZDOID left, ZDOID right) => !left.Equals(right);
}

public class Character
{
    public bool Owner { get; set; }
    public bool PlayerCharacter { get; set; }
    public float Health { get; set; }
    public ZDOID Id { get; set; }

    public bool IsOwner() => Owner;
    public bool IsPlayer() => PlayerCharacter;
    public float GetHealth() => Health;
    public ZDOID GetZDOID() => Id;
}

public sealed class Player : Character
{
}

public sealed class HitData
{
    public Character? Attacker { get; set; }
    public Character? GetAttacker() => Attacker;
}

public sealed class ZPackage
{
    private readonly MemoryStream stream;
    private readonly BinaryWriter writer;
    private readonly BinaryReader reader;

    public ZPackage()
        : this(Array.Empty<byte>())
    {
    }

    public ZPackage(byte[] bytes)
    {
        stream = new MemoryStream();
        writer = new BinaryWriter(stream);
        reader = new BinaryReader(stream);
        if (bytes.Length > 0)
        {
            writer.Write(bytes);
            stream.Position = 0;
        }
    }

    public byte[] GetArray() => stream.ToArray();
    public int GetPos() => checked((int)stream.Position);
    public int Size() => checked((int)stream.Length);

    public void Write(int value) => writer.Write(value);
    public void Write(long value) => writer.Write(value);
    public void Write(double value) => writer.Write(value);
    public void Write(bool value) => writer.Write(value);
    public void Write(string value) => writer.Write(value);
    public void Write(ZDOID value)
    {
        writer.Write(value.UserId);
        writer.Write(value.Id);
    }

    public void Write(UnityEngine.Vector3 value)
    {
        writer.Write(value.x);
        writer.Write(value.y);
        writer.Write(value.z);
    }

    public int ReadInt() => reader.ReadInt32();
    public long ReadLong() => reader.ReadInt64();
    public double ReadDouble() => reader.ReadDouble();
    public bool ReadBool() => reader.ReadBoolean();
    public string ReadString() => reader.ReadString();
    public ZDOID ReadZDOID() => new ZDOID(reader.ReadInt64(), reader.ReadUInt32());
    public UnityEngine.Vector3 ReadVector3() =>
        new UnityEngine.Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
}

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
