namespace BenheimQoL.PlayerCombat;

/// <summary>
/// Owns the one large native activation surface used by earned combat states.
/// Feature definitions choose copy, icon, sound, and effect details later.
/// </summary>
internal sealed class EarnedStatePresentation
{
    internal void ShowActivation(string message)
    {
        if (MessageHud.instance != null && !string.IsNullOrWhiteSpace(message))
        {
            MessageHud.instance.ShowBiomeFoundMsg(message, playStinger: false);
        }
    }
}
