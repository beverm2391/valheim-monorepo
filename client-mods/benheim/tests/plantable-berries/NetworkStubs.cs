using System;
using System.Collections.Generic;

internal sealed class ZNetView : UnityEngine.Component
{
    internal static readonly long Everybody = 0L;
    private static uint nextId = 1u;

    internal bool Owner = true;
    internal bool Valid = true;
    internal readonly List<(long Target, string Method, bool Value)> Invocations = new();
    private readonly ZDO zdo = CreateZdo();

    internal bool IsOwner() => Owner;
    internal bool IsValid() => Valid;
    internal ZDO GetZDO() => zdo;

    private static ZDO CreateZdo()
    {
        uint id = nextId++;
        return new ZDO(new ZDOID(9001L, id), new UnityEngine.Vector3(id, id * 0.5f, 0f));
    }

    internal void InvokeRPC(long target, string method, bool value)
    {
        Invocations.Add((target, method, value));
        if (method == "RPC_SetPicked")
        {
            gameObject.GetComponent<Pickable>()?.SetPicked(value);
        }
    }
}

internal sealed class Pickable : UnityEngine.Component
{
    internal UnityEngine.GameObject? m_itemPrefab;
    internal float m_respawnTimeMinutes;
    internal bool Picked { get; private set; }
    internal long PickedTime { get; private set; }
    internal bool Enabled = true;
    internal int NativeHarvests;

    internal bool GetPicked() => Picked;
    internal bool CanBePicked() => !Picked && Enabled;

    internal void LoadPickedState(bool picked) => Picked = picked;

    internal void SetPicked(bool picked)
    {
        BenheimQoL.Farming.BerryPickedDiagnosticPatch.Prefix(this, out var observation);
        Picked = picked;
        if (picked && m_respawnTimeMinutes > 0f && GetComponent<ZNetView>()!.IsOwner())
        {
            PickedTime = ZNet.instance.GetTime().Ticks;
            GetComponent<ZNetView>()!.GetZDO().Set(ZDOVars.s_pickedTime, PickedTime);
        }
        BenheimQoL.Farming.BerryPickedDiagnosticPatch.Postfix(this, observation);
    }

    // Matches the inspected native RPC's owner/picked gates and synchronous
    // local SetPicked ordering. This fixture does not simulate growth or drops.
    internal void Harvest(long sender)
    {
        BenheimQoL.Farming.BerryHarvestDiagnosticPatch.Prefix(this, out var observation);
        if (GetComponent<ZNetView>()!.IsOwner() && !Picked)
        {
            NativeHarvests++;
            SetPicked(true);
        }
        BenheimQoL.Farming.BerryHarvestDiagnosticPatch.Postfix(this, sender, observation);
    }

    internal bool ShouldRespawn(DateTime now)
    {
        long pickedTime = GetComponent<ZNetView>()!.GetZDO().GetLong(ZDOVars.s_pickedTime, 0L);
        return pickedTime <= 1L
            || (now - new DateTime(pickedTime)).TotalMinutes > m_respawnTimeMinutes;
    }
}

internal readonly struct ZDOID
{
    internal ZDOID(long userId, uint id)
    {
        UserID = userId;
        ID = id;
    }

    internal long UserID { get; }
    internal uint ID { get; }
    public override string ToString() => $"{UserID}:{ID}";
}

internal sealed class ZDO
{
    private readonly Dictionary<int, long> longs = new();

    internal ZDO(ZDOID id, UnityEngine.Vector3 position)
    {
        m_uid = id;
        Position = position;
    }

    internal ZDOID m_uid;
    internal UnityEngine.Vector3 Position;
    internal UnityEngine.Vector3 GetPosition() => Position;
    internal long GetLong(int key, long defaultValue = 0L) => longs.TryGetValue(key, out long value) ? value : defaultValue;
    internal void Set(int key, long value) => longs[key] = value;
}

internal static class ZDOVars
{
    internal const int s_creator = 1;
    internal const int s_pickedTime = 2;
}

internal sealed class ZNet : UnityEngine.Object
{
    internal static readonly ZNet instance = new();
    internal DateTime Time = new(638900000000000000L);
    internal long WorldId = 123L;
    internal bool FailWorldLookup;
    internal DateTime GetTime() => Time;
    internal long GetWorldUID() => FailWorldLookup
        ? throw new InvalidOperationException("test observation unavailable")
        : WorldId;
}
