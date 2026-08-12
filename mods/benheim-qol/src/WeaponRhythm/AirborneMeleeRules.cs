namespace BenheimQoL.WeaponRhythm;

internal static class AirborneMeleeRules
{
    internal static bool Qualifies(
        bool targetIsCharacter,
        bool attackerIsLocalPlayer,
        bool attackerIsAirborne)
    {
        return targetIsCharacter && attackerIsLocalPlayer && attackerIsAirborne;
    }
}
