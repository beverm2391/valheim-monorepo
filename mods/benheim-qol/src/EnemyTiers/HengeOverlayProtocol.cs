using System;

namespace BenheimQoL.EnemyTiers;

internal static class HengeOverlayProtocol
{
    internal const string RequestRpc = "Benheim.Test.HengeOverlay.Request.v1";
    internal const string ResultRpc = "Benheim.Test.HengeOverlay.Result.v1";
    internal const string Usage = "bh henge on|off";

    internal static bool TryParse(string[] arguments, out bool enabled)
    {
        enabled = false;
        if (arguments.Length != 3 ||
            !string.Equals(arguments[0], "bh", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(arguments[1], "henge", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(arguments[2], "on", StringComparison.OrdinalIgnoreCase))
        {
            enabled = true;
            return true;
        }

        return string.Equals(arguments[2], "off", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsHengeLocation(string prefabName)
    {
        return string.Equals(prefabName, "StoneHenge1", StringComparison.Ordinal) ||
            string.Equals(prefabName, "StoneHenge3", StringComparison.Ordinal) ||
            string.Equals(prefabName, "StoneHenge4", StringComparison.Ordinal) ||
            string.Equals(prefabName, "StoneHenge5", StringComparison.Ordinal);
    }
}
