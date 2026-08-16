using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Shortcuts;

internal static class NativeConsoleShortcut
{
    internal static void Update()
    {
        if (!InputState.IsKeyDown(KeyCode.Slash))
        {
            return;
        }

        Console? console = Console.instance;
        Player? player = Player.m_localPlayer;
        if (console == null
            || player == null
            || !console.IsConsoleEnabled()
            || !CanOpenDuringGameplay(player))
        {
            return;
        }

        // Open Valheim's own console and hand focus to its native input. From
        // here, Console.Update continues to own F5/Escape and every close path.
        console.m_chatWindow.gameObject.SetActive(true);
        console.m_input.ActivateInputField();
    }

    private static bool CanOpenDuringGameplay(Player player)
    {
        ZNet? network = ZNet.instance;
        return network != null
            && !network.InPasswordDialog()
            && !network.InConnectingScreen()
            && (Chat.instance == null || !Chat.instance.HasFocus())
            && !Console.IsVisible()
            && !TextInput.IsVisible()
            && !StoreGui.IsVisible()
            && !InventoryGui.IsVisible()
            && !Menu.IsVisible()
            && (TextViewer.instance == null || !TextViewer.instance.IsVisible())
            && !Minimap.IsOpen()
            && !GameCamera.InFreeFly()
            && !PlayerCustomizaton.IsBarberGuiVisible()
            && !Hud.IsPieceSelectionVisible()
            && !Hud.InRadial()
            && !UnifiedPopup.IsVisible()
            && !player.IsDead()
            && !player.InCutscene()
            && !player.IsTeleporting();
    }
}
