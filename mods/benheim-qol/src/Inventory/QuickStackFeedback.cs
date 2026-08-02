using System.Collections;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackFeedback
{
    private const float AbovePlayerDurationSeconds = 6f;
    private const float TopLeftRefreshDelaySeconds = 3f;

    internal static void ShowResult(
        Player player,
        bool inventoryWasOpen,
        int movedItems,
        string topLeftMessage)
    {
        player.Message(MessageHud.MessageType.TopLeft, topLeftMessage);
        player.StartCoroutine(RefreshTopLeftIfStillVisible(player, topLeftMessage));

        if (!inventoryWasOpen)
        {
            WorldFeedback.ShowAbovePlayer(
                player,
                QuickStackMessages.AbovePlayerSummary(movedItems),
                AbovePlayerDurationSeconds);
        }
    }

    private static IEnumerator RefreshTopLeftIfStillVisible(Player player, string message)
    {
        yield return new WaitForSecondsRealtime(TopLeftRefreshDelaySeconds);

        MessageHud messageHud = MessageHud.instance;
        if (!player || !messageHud || messageHud.m_messageText.text != Localization.instance.Localize(message))
        {
            yield break;
        }

        player.Message(MessageHud.MessageType.TopLeft, message);
    }
}
