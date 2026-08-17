using System;
using UnityEngine;

namespace BenheimQoL.KillAttribution;

/// <summary>
/// The deliberately small wire contract between a victim's authoritative
/// owner and Benheim Server Support. The report contains only identities: the
/// server derives victim metadata from its ZDO and never accepts a client-
/// supplied chain count or reward decision.
/// </summary>
internal static class KillAttributionProtocol
{
    internal const int Version = 1;
    internal const string CapabilityRpc = "Benheim_Kill_Capability_V1";
    internal const string ReportRpc = "Benheim_Kill_Report_V1";
    internal const string ConfirmedRpc = "Benheim_Kill_Confirmed_V1";

    private const int OperationIdLength = 32;
    private const int MaximumPrefabNameLength = 128;

    internal static ZPackage BuildReport(
        string operationId,
        ZDOID victimId,
        ZDOID killerId)
    {
        ZPackage package = new ZPackage();
        package.Write(Version);
        package.Write(operationId);
        package.Write(victimId);
        package.Write(killerId);
        return package;
    }

    internal static bool TryReadReport(
        ZPackage package,
        out KillReport report)
    {
        report = default;
        try
        {
            int version = package.ReadInt();
            string operationId = package.ReadString();
            ZDOID victimId = package.ReadZDOID();
            ZDOID killerId = package.ReadZDOID();
            if (version != Version
                || !Guid.TryParseExact(operationId, "N", out _)
                || operationId.Length != OperationIdLength
                || victimId.IsNone()
                || killerId.IsNone()
                || victimId == killerId
                || package.GetPos() != package.Size())
            {
                return false;
            }

            report = new KillReport(operationId, victimId, killerId);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static ZPackage BuildConfirmation(ConfirmedKillMessage message)
    {
        ZPackage package = new ZPackage();
        package.Write(Version);
        package.Write(message.OperationId);
        package.Write(message.VictimId);
        package.Write(message.KillerId);
        package.Write(message.VictimPrefabHash);
        package.Write(message.VictimPrefabName);
        package.Write(message.VictimLevel);
        package.Write(message.VictimIsBoss);
        package.Write(message.VictimIsTamed);
        package.Write(message.Position);
        package.Write(message.ServerSequence);
        package.Write(message.ServerTimeSeconds);
        return package;
    }

    internal static bool TryReadConfirmation(
        ZPackage package,
        out ConfirmedKillMessage message)
    {
        message = default;
        try
        {
            int version = package.ReadInt();
            string operationId = package.ReadString();
            ZDOID victimId = package.ReadZDOID();
            ZDOID killerId = package.ReadZDOID();
            int prefabHash = package.ReadInt();
            string prefabName = package.ReadString();
            int level = package.ReadInt();
            bool isBoss = package.ReadBool();
            bool isTamed = package.ReadBool();
            Vector3 position = package.ReadVector3();
            long sequence = package.ReadLong();
            double serverTimeSeconds = package.ReadDouble();
            if (version != Version
                || !Guid.TryParseExact(operationId, "N", out _)
                || operationId.Length != OperationIdLength
                || victimId.IsNone()
                || killerId.IsNone()
                || victimId == killerId
                || string.IsNullOrEmpty(prefabName)
                || prefabName.Length > MaximumPrefabNameLength
                || level < 1
                || sequence < 1
                || double.IsNaN(serverTimeSeconds)
                || double.IsInfinity(serverTimeSeconds)
                || serverTimeSeconds < 0d
                || package.GetPos() != package.Size())
            {
                return false;
            }

            message = new ConfirmedKillMessage(
                operationId,
                victimId,
                killerId,
                prefabHash,
                prefabName,
                level,
                isBoss,
                isTamed,
                position,
                sequence,
                serverTimeSeconds);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

internal readonly struct KillReport
{
    internal KillReport(string operationId, ZDOID victimId, ZDOID killerId)
    {
        OperationId = operationId;
        VictimId = victimId;
        KillerId = killerId;
    }

    internal string OperationId { get; }
    internal ZDOID VictimId { get; }
    internal ZDOID KillerId { get; }
}

internal readonly struct ConfirmedKillMessage
{
    internal ConfirmedKillMessage(
        string operationId,
        ZDOID victimId,
        ZDOID killerId,
        int victimPrefabHash,
        string victimPrefabName,
        int victimLevel,
        bool victimIsBoss,
        bool victimIsTamed,
        Vector3 position,
        long serverSequence,
        double serverTimeSeconds)
    {
        OperationId = operationId;
        VictimId = victimId;
        KillerId = killerId;
        VictimPrefabHash = victimPrefabHash;
        VictimPrefabName = victimPrefabName;
        VictimLevel = victimLevel;
        VictimIsBoss = victimIsBoss;
        VictimIsTamed = victimIsTamed;
        Position = position;
        ServerSequence = serverSequence;
        ServerTimeSeconds = serverTimeSeconds;
    }

    internal string OperationId { get; }
    internal ZDOID VictimId { get; }
    internal ZDOID KillerId { get; }
    internal int VictimPrefabHash { get; }
    internal string VictimPrefabName { get; }
    internal int VictimLevel { get; }
    internal bool VictimIsBoss { get; }
    internal bool VictimIsTamed { get; }
    internal Vector3 Position { get; }
    internal long ServerSequence { get; }
    internal double ServerTimeSeconds { get; }
}
