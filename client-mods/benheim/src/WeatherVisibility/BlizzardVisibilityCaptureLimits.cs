using System;

namespace BenheimQoL.WeatherVisibility;

internal static class BlizzardVisibilityCaptureLimits
{
    internal const int MaximumRoots = 16;
    internal const int MaximumNodesPerRoot = 96;
    internal const int MaximumTotalNodes = 256;
    internal const int MaximumComponentsPerNode = 16;
    internal const int MaximumMaterialsPerRenderer = 4;

    internal static int WrittenRoots(int available) =>
        Math.Min(Math.Max(available, 0), MaximumRoots);

    internal static int WrittenNodes(int available, int alreadyWritten)
    {
        int remaining = Math.Max(0, MaximumTotalNodes - Math.Max(alreadyWritten, 0));
        return Math.Min(
            Math.Max(available, 0),
            Math.Min(MaximumNodesPerRoot, remaining));
    }

    internal static int WrittenComponents(int available) =>
        Math.Min(Math.Max(available, 0), MaximumComponentsPerNode);

    internal static int WrittenMaterials(int available) =>
        Math.Min(Math.Max(available, 0), MaximumMaterialsPerRenderer);
}
