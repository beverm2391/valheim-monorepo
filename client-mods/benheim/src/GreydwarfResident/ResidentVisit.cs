namespace BenheimQoL.GreydwarfResident;

internal enum ResidentReaction
{
    None,
    Notice,
    Acknowledge
}

/// <summary>
/// One local visit, including the wider departure boundary. Crossing the near
/// radius or getting up and sitting again must not repeatedly greet a visitor.
/// This is session state, not player identity or resident memory.
/// </summary>
internal sealed class ResidentVisit
{
    private bool visiting;
    private bool seatedBefore;
    private bool acknowledged;
    private float awaySince = -1f;

    internal static bool IsOutside(float horizontalDistance, float verticalDistance)
    {
        return horizontalDistance > 6f || System.Math.Abs(verticalDistance) > 3f;
    }

    internal ResidentReaction Observe(
        float now, float horizontalDistance, float verticalDistance,
        bool seated, bool seed, bool canNotice)
    {
        if (IsOutside(horizontalDistance, verticalDistance))
        {
            if (awaySince < 0f) awaySince = now;
            if (now - awaySince >= 8f)
            {
                visiting = false;
                acknowledged = false;
            }
            seatedBefore = seated;
            return ResidentReaction.None;
        }

        awaySince = -1f;
        ResidentReaction reaction = ResidentReaction.None;
        if (horizontalDistance <= 4.5f && !visiting)
        {
            visiting = true;
            if (!seed && canNotice) reaction = ResidentReaction.Notice;
        }
        if (seated && !seatedBefore && !acknowledged)
        {
            acknowledged = true;
            if (!seed) reaction = ResidentReaction.Acknowledge;
        }
        seatedBefore = seated;
        // Enabling beside someone already soaking must not greet them.
        if (seed && seated) acknowledged = true;
        return reaction;
    }
}
