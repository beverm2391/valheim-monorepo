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
    }
}

internal sealed class SpawnSystem : UnityEngine.Object
{
    internal sealed class SpawnData
    {
        internal UnityEngine.GameObject m_prefab = null!;
        internal float m_spawnInterval;
    }

    internal static bool m_nospawn;

    internal SpawnSystem(SpawnData spawner)
    {
        m_spawnLists.Add(new SpawnSystemList(spawner));
    }

    internal List<SpawnSystemList> m_spawnLists { get; } = new();
    internal int SuccessfulSpawns { get; private set; }
    internal bool ThrowOnSpawn { get; set; }

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
