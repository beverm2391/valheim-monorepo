using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Shortcuts;

internal static class NativeConsoleShortcut
{
    internal static void Update()
    {
        if (!RawSlashDown())
        {
            return;
        }

        Console? console = Console.instance;
        Player? player = Player.m_localPlayer;
        string? rejection = RejectionReason(console, player);
        if (rejection != null)
        {
            EmitResult("rejected", rejection, console);
            return;
        }

        // Open Valheim's own console and hand focus to its native input. From
        // here, Console.Update continues to own F5/Escape and every close path.
        Console nativeConsole = console!;
        nativeConsole.m_chatWindow.gameObject.SetActive(true);
        nativeConsole.m_input.ActivateInputField();
        EmitResult("opened", "normal_gameplay", nativeConsole);
    }

    private static bool RawSlashDown()
    {
        // InputState intentionally suppresses shortcuts during text entry.
        // This owner needs the raw key-down only so it can record that exact
        // rejection. It still performs no action until every native gate passes.
        return Input.GetKeyDown(KeyCode.Slash) || ZInput.GetKeyDown(KeyCode.Slash);
    }

    private static string? RejectionReason(Console? console, Player? player)
    {
        if (ShortcutOverlay.IsOpen)
        {
            return "benheim_menu";
        }
        if (Console.IsVisible())
        {
            return "native_console_visible";
        }
        if (Minimap.InTextInput())
        {
            return "map_text_input";
        }
        if (TextInput.IsVisible())
        {
            return "native_text_input";
        }
        if (InputState.IsTextEntryActive())
        {
            return "focused_text_field";
        }
        if (console == null)
        {
            return "console_unavailable";
        }
        if (player == null)
        {
            return "player_unavailable";
        }
        if (!console.IsConsoleEnabled())
        {
            return "console_disabled";
        }

        ZNet? network = ZNet.instance;
        if (network == null) return "network_unavailable";
        if (network.InPasswordDialog()) return "password_dialog";
        if (network.InConnectingScreen()) return "connecting_screen";
        if (Chat.instance != null && Chat.instance.HasFocus()) return "chat_focused";
        if (StoreGui.IsVisible()) return "store";
        if (InventoryGui.IsVisible()) return "inventory";
        if (Menu.IsVisible()) return "menu";
        if (TextViewer.instance != null && TextViewer.instance.IsVisible()) return "text_viewer";
        if (Minimap.IsOpen()) return "map_open";
        if (GameCamera.InFreeFly()) return "free_fly";
        if (PlayerCustomizaton.IsBarberGuiVisible()) return "barber";
        if (Hud.IsPieceSelectionVisible()) return "piece_selection";
        if (Hud.InRadial()) return "radial_menu";
        if (UnifiedPopup.IsVisible()) return "popup";
        if (player.IsDead()) return "player_dead";
        if (player.InCutscene()) return "cutscene";
        if (player.IsTeleporting()) return "teleporting";
        return null;
    }

    private static void EmitResult(string result, string reason, Console? console)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("Shortcuts", "native_console_shortcut")
                .String("result", result)
                .String("reason", reason)
                .Boolean("console_available", console != null)
                .Boolean("console_enabled", console != null && console.IsConsoleEnabled()));
    }
}
