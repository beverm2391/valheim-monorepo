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

        presentation.BeginPerfectDefense(PlayerCombatContext.Capture(player));
        AdrenalineFeedback.ShowAward(
            player,
            new AdrenalineFeedback.Award("Perfect dodge", before: 0f, maximum: 0f));
        presentation.BeginPerfectDefense(PlayerCombatContext.Capture(player));
        AdrenalineFeedback.ShowAward(
            player,
            new AdrenalineFeedback.Award("Perfect parry", before: 0f, maximum: 0f));

        Expect(WorldFeedback.Messages.Count == 2,
            "locked native adrenaline still emits one message per confirmed defense");
        Expect(WorldFeedback.Messages[0] == "Perfect dodge"
                && WorldFeedback.Messages[1] == "Perfect parry",
            "locked native adrenaline omits a misleading numeric gain");

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
