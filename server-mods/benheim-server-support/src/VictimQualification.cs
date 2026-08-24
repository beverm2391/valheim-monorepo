namespace BenheimServerSupport;

/// <summary>
/// Uses the smallest native prefab facts that distinguish hostile creatures
/// from passive hunting. Deer are AnimalAI. Boar is the one unavoidable native
/// exception because its prefab is MonsterAI + ForestMonsters. Tame state stays
/// authoritative on the defeated victim ZDO.
/// </summary>
internal static class VictimQualification
{
    internal static bool IsHostileCreature(
        Character.Faction faction,
        bool isBoss,
        bool isTamed,
        bool hasMonsterAi,
        bool isCanonicalBoar)
    {
        if (isTamed)
        {
            return false;
        }

        if (isBoss)
        {
            return true;
        }

        if (!hasMonsterAi || isCanonicalBoar)
        {
            return false;
        }

        return faction == Character.Faction.ForestMonsters
            || faction == Character.Faction.Undead
            || faction == Character.Faction.Demon
            || faction == Character.Faction.MountainMonsters
            || faction == Character.Faction.SeaMonsters
            || faction == Character.Faction.PlainsMonsters
            || faction == Character.Faction.MistlandsMonsters;
    }
}
