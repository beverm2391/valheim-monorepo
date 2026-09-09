namespace BenheimQoL.Interaction;

internal static class FeastInteractionRange
{
    internal static float Resolve(float currentUseDistance)
    {
        return currentUseDistance < InteractionRanges.UseDistance
            ? InteractionRanges.UseDistance
            : currentUseDistance;
    }
}
