using Benheim.Resident;
using BenheimServerSupport;

Expect(ResidentProtocol.Version == 2, "the trigger field uses the v2 transient contract");
Expect(ResidentProtocol.InvitedKey.EndsWith("_v1", StringComparison.Ordinal), "saved invitation keys retain their v1 identity");
Expect(ResidentProtocol.TryParseEncounterTrigger(0, out var approach) && approach == ResidentEncounterTrigger.Approach,
    "wire value 0 is the approach trigger");
Expect(ResidentProtocol.TryParseEncounterTrigger(1, out var talk) && talk == ResidentEncounterTrigger.Talk,
    "wire value 1 is the manual talk trigger");
Expect(!ResidentProtocol.TryParseEncounterTrigger(-1, out _), "negative trigger values are rejected");
Expect(!ResidentProtocol.TryParseEncounterTrigger(2, out _), "unknown trigger values are rejected");

var cadence = new ResidentEncounterCadence();
Expect(cadence.Evaluate(ResidentEncounterTrigger.Approach, 0, null) == ResidentEncounterAdmission.Granted,
    "an initial ambient request is admitted");
cadence.RecordGrant(ResidentEncounterTrigger.Approach, 0);
Expect(cadence.Evaluate(ResidentEncounterTrigger.Talk, 1, null) == ResidentEncounterAdmission.Granted,
    "manual Talk has its own gate and can pass during the ambient cooldown");
cadence.RecordGrant(ResidentEncounterTrigger.Talk, 1);
Expect(cadence.Evaluate(ResidentEncounterTrigger.Approach, 1, null) == ResidentEncounterAdmission.Cooldown,
    "an accepted Talk does not reset the 45 second ambient cooldown");
Expect(cadence.Evaluate(ResidentEncounterTrigger.Talk, 3.999, null) == ResidentEncounterAdmission.Cooldown,
    "Talk remains gated until three seconds after its accepted request");
Expect(cadence.Evaluate(ResidentEncounterTrigger.Talk, 4, null) == ResidentEncounterAdmission.Granted,
    "Talk reopens at its exact three second boundary");
Expect(cadence.Evaluate(ResidentEncounterTrigger.Approach, 44.999, null) == ResidentEncounterAdmission.Cooldown,
    "ambient requests remain gated for 45 seconds");
Expect(cadence.Evaluate(ResidentEncounterTrigger.Approach, 45, null) == ResidentEncounterAdmission.Granted,
    "ambient requests reopen at the exact 45 second boundary");

var talkSuppressesAmbient = new ResidentEncounterCadence();
talkSuppressesAmbient.RecordGrant(ResidentEncounterTrigger.Talk, 0);
Expect(talkSuppressesAmbient.Evaluate(ResidentEncounterTrigger.Approach, 2.999, null) == ResidentEncounterAdmission.Cooldown,
    "a manual line suppresses an immediate ambient follow-up");
Expect(talkSuppressesAmbient.Evaluate(ResidentEncounterTrigger.Approach, 3, null) == ResidentEncounterAdmission.Granted,
    "ambient behavior resumes after the short post-Talk suppression window");

var activeSlot = new ResidentEncounterCadence();
Expect(activeSlot.Evaluate(ResidentEncounterTrigger.Talk, 0, pendingExpiresAt: 12) == ResidentEncounterAdmission.EncounterInProgress,
    "a pending ambient encounter blocks a second trigger on the same tub");
Expect(activeSlot.Evaluate(ResidentEncounterTrigger.Approach, 0, pendingExpiresAt: 12) == ResidentEncounterAdmission.EncounterInProgress,
    "the active slot blocks concurrent requests regardless of trigger type");
Expect(activeSlot.Evaluate(ResidentEncounterTrigger.Talk, 11.999, pendingExpiresAt: 12) == ResidentEncounterAdmission.EncounterInProgress,
    "the active slot stays occupied until its timeout");
Expect(activeSlot.Evaluate(ResidentEncounterTrigger.Talk, 12, pendingExpiresAt: 12) == ResidentEncounterAdmission.Granted,
    "the active slot can be reused at its exact expiry boundary");

activeSlot.RecordGrant(ResidentEncounterTrigger.Talk, 12);
Expect(!activeSlot.IsExpired(12), "an accepted Talk keeps cadence state alive");
activeSlot.Reset();
Expect(activeSlot.IsExpired(12), "dismissal reset clears all cadence gates");
Expect(activeSlot.Evaluate(ResidentEncounterTrigger.Approach, 12, null) == ResidentEncounterAdmission.Granted,
    "a reset cadence admits a fresh ambient request");

Console.WriteLine("Resident encounter arbitration tests passed.");

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
