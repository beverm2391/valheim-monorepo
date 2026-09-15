using System;
using System.Collections.Generic;

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

namespace BenheimQoL
{
    internal static class Plugin
    {
        internal static TestLog Log { get; } = new TestLog();
    }

    internal sealed class TestLog
    {
        internal void LogWarning(string message)
        {
        }
    }
}
