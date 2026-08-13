using System;

namespace BenheimQoL.EnemyTiers;

internal static class BoarTestCommandProtocol
{
    internal const string RequestRpc = "Benheim.Test.SpawnBoar.Request.v1";
    internal const string ResultRpc = "Benheim.Test.SpawnBoar.Result.v1";
    internal const string Usage = "benheim spawn-boar 0|1|2";

    internal static bool TryParse(string[] arguments, out int stars)
    {
        stars = 0;
        return arguments.Length == 3 &&
            string.Equals(arguments[0], "benheim", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(arguments[1], "spawn-boar", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(arguments[2], out stars) &&
            (stars == 0 || stars == 1 || stars == 2);
    }

    internal static bool TryResolveLevel(int stars, out int level)
    {
        switch (stars)
        {
            case 0:
                level = 1;
                return true;
            case 1:
                level = 2;
                return true;
            case 2:
                level = 3;
                return true;
            default:
                level = 0;
                return false;
        }
    }
}
