using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UnityEngine
{
    internal class Object
    {
        public static implicit operator bool(Object? value) => value != null;
    }

    internal sealed class GameObject : Object
    {
        internal GameObject(string name)
        {
            this.name = name;
        }

        internal string name { get; }
    }

    internal readonly struct Vector3
    {
        internal static Vector3 zero => new();
    }

    internal static class Time
    {
        internal static float realtimeSinceStartup { get; set; }
    }
}

internal static class Heightmap
{
    [Flags]
    internal enum Biome
    {
        None = 0,
        Swamp = 1,
    }

    [Flags]
    internal enum BiomeArea
    {
        None = 0,
        Everything = 1,
    }
}

internal sealed class SpawnSystem : UnityEngine.Object
{
    internal sealed class SpawnData
    {
        internal UnityEngine.GameObject m_prefab = null!;
        internal float m_spawnInterval;
        internal float m_spawnChance;
        internal int m_maxSpawned;
        internal int m_groupSizeMin;
        internal int m_groupSizeMax;
        internal float m_spawnDistance;
        internal Heightmap.Biome m_biome;
        internal Heightmap.BiomeArea m_biomeArea;
        internal float m_minAltitude;
        internal float m_maxAltitude;
    }

    internal static bool m_nospawn;

    internal SpawnSystem(SpawnData spawner)
    {
        m_spawnLists.Add(new SpawnSystemList(spawner));
    }

    internal List<SpawnSystemList> m_spawnLists { get; } = new();
    internal int SuccessfulSpawns { get; private set; }
    internal bool ThrowOnSpawn { get; set; }
    internal static int LoadedInstances { get; set; }
    internal static bool ThrowOnCount { get; set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Awake()
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Spawn(
        SpawnData critter,
        UnityEngine.Vector3 spawnPoint,
        bool eventSpawner,
        int minLevelOverride = -1,
        float levelUpMultiplier = 1f)
    {
        if (m_nospawn)
        {
            return;
        }

        if (ThrowOnSpawn)
        {
            throw new TestSpawnException();
        }

        SuccessfulSpawns++;
    }

    internal void InvokeAwake() => Awake();

    internal void InvokeSpawn(SpawnData spawner, bool eventSpawner)
    {
        Spawn(spawner, new UnityEngine.Vector3(), eventSpawner);
    }

    internal static int GetNrOfInstances(
        UnityEngine.GameObject prefab,
        UnityEngine.Vector3 center,
        float maxRange,
        bool eventCreaturesOnly = false,
        bool procreationOnly = false)
    {
        if (ThrowOnCount)
        {
            throw new InvalidOperationException("native population count failed");
        }
        return LoadedInstances;
    }
}

internal sealed class TestSpawnException : Exception
{
}

internal sealed class SpawnSystemList
{
    internal SpawnSystemList(SpawnSystem.SpawnData spawner)
    {
        m_spawners.Add(spawner);
    }

    internal List<SpawnSystem.SpawnData> m_spawners { get; } = new();
}

internal sealed class ZNetScene
{
    private readonly UnityEngine.GameObject leechPrefab;

    internal ZNetScene(UnityEngine.GameObject leechPrefab)
    {
        this.leechPrefab = leechPrefab;
    }

    internal static ZNetScene? instance;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Awake()
    {
    }

    internal UnityEngine.GameObject? GetPrefab(string name)
    {
        return name == leechPrefab.name ? leechPrefab : null;
    }
}

internal static class Plugin
{
    internal static TestLog Log { get; } = new();
}

internal sealed class TestLog
{
    internal void LogError(string message)
    {
    }
}

namespace BenheimQoL.Infrastructure
{
    internal sealed class DiagnosticEvent
    {
        private DiagnosticEvent(string domain, string name)
        {
            Domain = domain;
            Name = name;
        }

        internal string Domain { get; }
        internal string Name { get; }
        internal Dictionary<string, object> Fields { get; } = new();

        internal static DiagnosticEvent Create(string domain, string name)
        {
            return new DiagnosticEvent(domain, name);
        }

        internal DiagnosticEvent String(string name, string value)
        {
            Fields.Add(name, value);
            return this;
        }

        internal DiagnosticEvent Number(string name, float value)
        {
            Fields.Add(name, value);
            return this;
        }

        internal DiagnosticEvent Integer(string name, int value)
        {
            Fields.Add(name, value);
            return this;
        }

        internal DiagnosticEvent Boolean(string name, bool value)
        {
            Fields.Add(name, value);
            return this;
        }
    }

    internal static class Diagnostics
    {
        internal static List<DiagnosticEvent> Emitted { get; } = new();

        internal static void Event(string feature, string action, string details = "")
        {
        }

        internal static void Emit(DiagnosticEvent diagnosticEvent)
        {
            Emitted.Add(diagnosticEvent);
        }

        internal static void Reset()
        {
            Emitted.Clear();
        }
    }
}

namespace BenheimQoL.DeveloperDiagnostics
{
    internal static class DeveloperDiagnosticsRuntime
    {
        internal static List<string> Failures { get; } = new();

        internal static void RegisterEventProbe(
            string name,
            bool shippedDefault,
            DiagnosticProbeActivation setActive,
            Action update,
            Action<DiagnosticProbeCleanupReason> cleanup)
        {
        }

        internal static void ReportFailure(string lifecycle, string probe, string reason)
        {
            Failures.Add($"{lifecycle}:{probe}:{reason}");
        }
    }

    internal delegate bool DiagnosticProbeActivation(bool active, out string failure);

    internal enum DiagnosticProbeCleanupReason
    {
        Disabled,
        WorldExit,
        SessionReset,
        Failure,
    }
}
