// Bundled template; the runtime recipe is copied into VALHEIM_DEV_ROOT/registry.
using System;
using UnityEngine;

public static class ValheimDevChange
{
    [Serializable]
    public sealed class Input
    {
        public float dayFraction;
    }

    [Serializable]
    public sealed class Result
    {
        public bool enabled;
        public float dayFraction;
        public bool previousDebugTimeOfDay;
        public float previousDebugTime;
    }

    private static EnvMan env;
    private static bool previousDebugTimeOfDay;
    private static float previousDebugTime;

    public static string Run(string inputJson)
    {
        Input input = JsonUtility.FromJson<Input>(inputJson);
        env = EnvMan.instance;
        if (!env)
            throw new InvalidOperationException("EnvMan is unavailable.");

        previousDebugTimeOfDay = env.m_debugTimeOfDay;
        previousDebugTime = env.m_debugTime;
        env.m_debugTime = Mathf.Clamp01(input.dayFraction);
        env.m_debugTimeOfDay = true;

        return JsonUtility.ToJson(new Result {
            enabled = env.m_debugTimeOfDay,
            dayFraction = env.m_debugTime,
            previousDebugTimeOfDay = previousDebugTimeOfDay,
            previousDebugTime = previousDebugTime
        });
    }

    public static void Cleanup()
    {
        if (!env)
            return;
        env.m_debugTime = previousDebugTime;
        env.m_debugTimeOfDay = previousDebugTimeOfDay;
        env = null;
    }
}
