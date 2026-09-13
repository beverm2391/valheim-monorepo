using BenheimQoL.PlayerCombat;
using static TestSupport;

internal static class AdrenalineCombatGateTests
{
    internal static void Run()
    {
        Player player = new Player(20f, 100f)
        {
            MaximumAdrenaline = 0f
        };
        FakeOutput output = new FakeOutput();
        FactRecorder facts = new FactRecorder();
        PlayerCombatController controller = new PlayerCombatController(player, output, facts);

        controller.Observe(Defense(player, PerfectDefenseKind.Parry));
        controller.Observe(BerserkerTransition(
            player,
            BerserkerChainTransitionKind.Activated,
            BerserkerChainTier.Berserker,
            killCount: 6));
        bool directEarned = controller.Earn(
            PlayerCombatContext.Capture(player),
            EarnedCombatState.Untouchable,
            tier: 1);

        Expect(controller.UntouchableStreak == 0,
            "locked native adrenaline prevents defense and kill streak progress");
        Expect(!controller.HasEarned(EarnedCombatState.Clutch)
                && !controller.HasEarned(EarnedCombatState.Untouchable)
                && !controller.HasEarned(EarnedCombatState.Berserker),
            "locked native adrenaline prevents every earned combat state");
        Expect(!directEarned && output.Activations.Count == 0,
            "locked native adrenaline cannot reach native earned-state output");
        Expect(facts.Clutch.Count == 0
                && facts.Untouchable.Count == 0
                && facts.Transitions.Count == 0,
            "locked native adrenaline emits no earned-state decisions or feedback facts");
    }
}
