using System;

namespace BenheimQoL.EnemyTiers;

internal static class BoarTestCommandProtocol
{
    internal const string RequestRpc = "Benheim.Test.SpawnBoar.Request.v1";
    internal const string ResultRpc = "Benheim.Test.SpawnBoar.Result.v1";
    internal const string Usage = "bh spawn boar 0|1|2";

    internal static bool IsHelpRequest(string[] arguments)
    {
        return (arguments.Length == 1 &&
                string.Equals(arguments[0], "bh", StringComparison.OrdinalIgnoreCase)) ||
            (arguments.Length == 2 &&
                string.Equals(arguments[0], "bh", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(arguments[1], "help", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool TryParseSpawnBoar(string[] arguments, out int stars)
    {
        stars = 0;
        return arguments.Length == 4 &&
            string.Equals(arguments[0], "bh", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(arguments[1], "spawn", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(arguments[2], "boar", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(arguments[3], out stars) &&
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
