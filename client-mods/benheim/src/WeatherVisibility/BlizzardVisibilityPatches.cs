using HarmonyLib;

namespace BenheimQoL.WeatherVisibility;

internal static class BlizzardVisibilityPatches
{
    [HarmonyPatch(typeof(EnvMan), "SetEnv")]
    private static class SetEnvironmentPatch
    {
        private static void Prefix(ref EnvSetup env)
        {
            if (BlizzardVisibilityRuntime.ShouldApply(env))
            {
                env = BlizzardVisibilityRuntime.CreateVisualEnvironment(env);
            }
        }

        private static void Postfix(EnvSetup env) =>
            BlizzardVisibilityRuntime.Update(env);
    }

    [HarmonyPatch(typeof(EnvMan), "OnDestroy")]
    private static class EnvironmentDestroyPatch
    {
        private static void Prefix() =>
            BlizzardVisibilityRuntime.Reset("world_lost");
    }
}
