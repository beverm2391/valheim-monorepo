namespace BenheimQoL.PlayerCombat;

internal static class PerfectDefenseOutcomeIdentityTests
{
    internal static void Run()
    {
        PerfectDefenseOutcomeDeduplicator outcomes =
            new PerfectDefenseOutcomeDeduplicator();
        object firstNativeOutcome = new object();
        object secondNativeOutcome = new object();

        TestSupport.Expect(
            outcomes.TryAccept(firstNativeOutcome, out int firstToken),
            "the first blocked hit from a native outcome is accepted");
        TestSupport.Expect(
            !outcomes.TryAccept(firstNativeOutcome, out int duplicateToken)
                && duplicateToken == firstToken,
            "another blocked hit from the same native outcome is rejected by identity");
        TestSupport.Expect(
            outcomes.TryAccept(secondNativeOutcome, out int secondToken)
                && secondToken == firstToken + 1,
            "a different native outcome is accepted immediately without a time debounce");
        TestSupport.Expect(
            !outcomes.TryAccept(firstNativeOutcome, out int delayedDuplicateToken)
                && delayedDuplicateToken == firstToken,
            "a delayed A/B/A replay of the first outcome remains rejected");

        object firstProjectileHit = new object();
        object secondProjectileHit = new object();
        TestSupport.Expect(
            outcomes.TryAccept(firstProjectileHit, out _)
                && outcomes.TryAccept(secondProjectileHit, out _)
                && !outcomes.TryAccept(firstProjectileHit, out _),
            "distinct projectile hit outcomes are accepted while a repeated hit is rejected");

        NativeAttackOutcomeIdentities<object> attackOutcomes =
            new NativeAttackOutcomeIdentities<object>();
        object loopingAttack = new object();
        object firstTrigger = attackOutcomes.Begin(loopingAttack);
        TestSupport.Expect(
            ReferenceEquals(
                firstTrigger,
                attackOutcomes.GetOrBegin(loopingAttack, out bool firstObserved))
                && firstObserved,
            "repeated reports from one native attack trigger share an identity");
        object secondTrigger = attackOutcomes.Begin(loopingAttack);
        TestSupport.Expect(
            !ReferenceEquals(firstTrigger, secondTrigger),
            "a later trigger from the same looping attack gets a distinct identity");

        outcomes.Reset();
        TestSupport.Expect(
            outcomes.TryAccept(firstNativeOutcome, out int resetToken)
                && resetToken == 1,
            "a combat lifecycle reset clears retained native outcome identity");
    }
}
