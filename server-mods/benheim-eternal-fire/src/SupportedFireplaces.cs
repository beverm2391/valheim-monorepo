using System.Collections.Generic;

namespace BenheimEternalFire;

internal static class SupportedFireplaces
{
    private static readonly Dictionary<int, string> NamesByHash = BuildNamesByHash();

    internal static bool TryGetName(int prefabHash, out string name)
    {
        return NamesByHash.TryGetValue(prefabHash, out name!);
    }

    private static Dictionary<int, string> BuildNamesByHash()
    {
        string[] names =
        {
            "fire_pit",
            "fire_pit_iron",
            "bonfire",
            "hearth",
            "piece_walltorch",
            "piece_groundtorch",
            "piece_groundtorch_wood",
            "piece_groundtorch_green",
            "piece_groundtorch_blue",
            "piece_brazierfloor01",
            "piece_brazierfloor02",
            "piece_brazierceiling01",
            "piece_jackoturnip",
            "piece_bathtub"
        };

        Dictionary<int, string> result = new Dictionary<int, string>();
        foreach (string name in names)
        {
            result.Add(name.GetStableHashCode(), name);
        }

        return result;
    }
}
