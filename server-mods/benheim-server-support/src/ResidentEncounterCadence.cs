using System;
using Benheim.Resident;

namespace BenheimServerSupport;

internal enum ResidentEncounterAdmission
{
    Granted,
    EncounterInProgress,
    Cooldown,
    InvalidTrigger
}

/// <summary>
/// Per-tub trigger gates. The server's PendingEncounters dictionary remains
/// the sole owner of active requests; this class only owns the independent
/// cadence windows used to admit a new request.
/// </summary>
internal sealed class ResidentEncounterCadence
{
    internal const double ApproachCooldownSeconds = 45d;
    internal const double TalkCooldownSeconds = 3d;

    private double approachCooldownUntil;
    private double talkCooldownUntil;
    private double approachSuppressedUntil;

    internal ResidentEncounterAdmission Evaluate(
        ResidentEncounterTrigger trigger,
        double now,
        double? pendingExpiresAt)
    {
        if (trigger != ResidentEncounterTrigger.Approach && trigger != ResidentEncounterTrigger.Talk)
        {
            return ResidentEncounterAdmission.InvalidTrigger;
        }

        if (pendingExpiresAt.HasValue && now < pendingExpiresAt.Value)
        {
            return ResidentEncounterAdmission.EncounterInProgress;
        }

        if (trigger == ResidentEncounterTrigger.Approach)
        {
            return now < approachCooldownUntil || now < approachSuppressedUntil
                ? ResidentEncounterAdmission.Cooldown
                : ResidentEncounterAdmission.Granted;
        }

        return now < talkCooldownUntil
            ? ResidentEncounterAdmission.Cooldown
            : ResidentEncounterAdmission.Granted;
    }

    /// <summary>Call once, after the server has reserved its active slot.</summary>
    internal void RecordGrant(ResidentEncounterTrigger trigger, double now)
    {
        switch (trigger)
        {
            case ResidentEncounterTrigger.Approach:
                approachCooldownUntil = now + ApproachCooldownSeconds;
                break;
            case ResidentEncounterTrigger.Talk:
                talkCooldownUntil = now + TalkCooldownSeconds;
                // A manual request can speak immediately, but must not be
                // followed by an ambient approach line on the next update.
                approachSuppressedUntil = now + TalkCooldownSeconds;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "Unknown resident encounter trigger.");
        }
    }

    internal bool IsExpired(double now) =>
        now >= approachCooldownUntil &&
        now >= talkCooldownUntil &&
        now >= approachSuppressedUntil;

    internal void Reset()
    {
        approachCooldownUntil = 0d;
        talkCooldownUntil = 0d;
        approachSuppressedUntil = 0d;
    }
}
