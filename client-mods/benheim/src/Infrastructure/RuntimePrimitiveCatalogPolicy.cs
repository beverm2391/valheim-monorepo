using System;
using System.Reflection;

namespace BenheimQoL.Infrastructure;

internal static class RuntimePrimitiveCatalogPolicy
{
    internal static bool IsNativeRuntimeType(
        Assembly candidateAssembly,
        Assembly nativeAssembly)
    {
        return candidateAssembly == nativeAssembly;
    }

    internal static bool IsPluginOwnedObjectName(string name)
    {
        return name.StartsWith("Benheim", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("BepInEx", StringComparison.OrdinalIgnoreCase);
    }

    internal static string StableFontIdentity(
        string fontName,
        string atlasIdentity,
        string materialIdentity)
    {
        return $"{fontName}|atlas={atlasIdentity}|material={materialIdentity}";
    }

    internal static string StableMaterialIdentity(
        string materialName,
        string shaderIdentity,
        string textureIdentity)
    {
        return $"{materialName}|shader={shaderIdentity}|texture={textureIdentity}";
    }
}
