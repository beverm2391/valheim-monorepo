namespace BenheimQoL.Adrenaline;

/// <summary>
/// Owns Benheim's boundary for the native adrenaline-combat layer. Valheim
/// exposes no usable adrenaline pool until the current player has positive
/// capacity, so Benheim must leave both grants and earned combat states inert.
/// </summary>
internal static class AdrenalineAvailability
{
    internal static bool IsUnlocked(Player player)
    {
        return player.GetMaxAdrenaline() > 0f;
    }
}
