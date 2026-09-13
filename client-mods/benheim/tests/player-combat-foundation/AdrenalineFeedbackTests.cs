using BenheimQoL.Adrenaline;
using BenheimQoL.Infrastructure;
using BenheimQoL.PlayerCombat;
using static TestSupport;

internal static class AdrenalineFeedbackTests
{
    internal static void Run()
    {
        WorldFeedback.Reset();
        Player player = new Player(100f, 100f)
        {
            Adrenaline = 0f,
            MaximumAdrenaline = 0f
        };
        Player.m_localPlayer = player;
        EarnedStatePresentation presentation = new EarnedStatePresentation();
        PlayerCombatRuntime.Presentation = presentation;

        PlayerCombatContext lockedDefense = PlayerCombatContext.Capture(player);
        presentation.BeginPerfectDefense(lockedDefense);
        presentation.Observe(
            new EarnedStateTransition(
                lockedDefense,
                EarnedCombatState.Clutch,
                tier: 1,
                EarnedStateTransitionKind.Activated,
                EarnedStateTransitionReason.NativeEffectApplied));
        AdrenalineFeedback.ShowAward(
            player,
            new AdrenalineFeedback.Award("Perfect dodge", before: 0f, maximum: 0f));
        presentation.BeginPerfectDefense(PlayerCombatContext.Capture(player));
        AdrenalineFeedback.ShowAward(
            player,
            new AdrenalineFeedback.Award("Perfect parry", before: 0f, maximum: 0f));

        Expect(WorldFeedback.Messages.Count == 0,
            "locked native adrenaline suppresses perfect-defense feedback");
        Expect(!AdrenalineAvailability.IsUnlocked(player),
            "zero native capacity keeps the adrenaline-combat layer dormant");
        Expect(AdrenalineFeedback.CaptureAward(player, 10f) == null,
            "locked native adrenaline cannot capture a perfect-defense award");

        player.MaximumAdrenaline = 100f;
        Expect(AdrenalineAvailability.IsUnlocked(player),
            "positive native capacity unlocks the adrenaline-combat layer");

        int messagesBeforeOrdinaryDefense = WorldFeedback.Messages.Count;
        AdrenalineFeedback.Reset();
        Expect(AdrenalineFeedback.CaptureAward(player, 10f) == null,
            "an ordinary defense or roll cannot open perfect-defense feedback");
        AdrenalineFeedback.ShowAward(player, award: null);
        Expect(WorldFeedback.Messages.Count == messagesBeforeOrdinaryDefense,
            "ordinary defenses and rolls remain silent");
        PlayerCombatRuntime.Presentation = null;
    }
}
