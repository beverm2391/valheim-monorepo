#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
shortcut="$root/src/Shortcuts/NativeConsoleShortcut.cs"
plugin="$root/src/Plugin.cs"
catalog="$root/src/Shortcuts/ShortcutOverlayCatalog.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_console="$source_tree/Console.cs"
native_player="$source_tree/Player.cs"

# Valheim owns enablement, presentation, focus, and the F5/Escape toggle.
grep -Fq 'SetConsoleEnabled(PlatformPrefs.GetInt("EnableConsole") == 1);' "$native_console"
grep -Fq 'm_chatWindow.gameObject.SetActive(!m_chatWindow.gameObject.activeSelf);' "$native_console"
grep -Fq 'm_input.ActivateInputField();' "$native_console"
grep -Fq 'protected override bool TakeInput()' "$native_player"

# Benheim adds only an open request after gameplay health has passed.
grep -Fq 'NativeConsoleShortcut.Update();' "$plugin"
grep -Fq 'Input.GetKeyDown(KeyCode.Slash) || ZInput.GetKeyDown(KeyCode.Slash)' "$shortcut"
grep -Fq '!console.IsConsoleEnabled()' "$shortcut"
grep -Fq 'nativeConsole.m_chatWindow.gameObject.SetActive(true);' "$shortcut"
grep -Fq 'nativeConsole.m_input.ActivateInputField();' "$shortcut"
grep -Fq 'DiagnosticEvent.Create("Shortcuts", "native_console_shortcut")' "$shortcut"
grep -Fq '.String("result", result)' "$shortcut"
grep -Fq '.String("reason", reason)' "$shortcut"
grep -Fq 'EmitResult("opened", "normal_gameplay", nativeConsole);' "$shortcut"
! grep -Fq 'SetConsoleEnabled(' "$shortcut"
! grep -Fq 'SetConsoleEnabledForThisSession' "$shortcut"
! grep -Fq 'SetActive(false)' "$shortcut"
! grep -Fq 'SetActive(!' "$shortcut"

# The helper repeats the explicit chat, password, and menu boundaries that are
# most important for this shortcut. InputState owns the shared text-entry gate.
grep -Fq 'network.InPasswordDialog()' "$shortcut"
grep -Fq 'network.InConnectingScreen()' "$shortcut"
grep -Fq 'Chat.instance.HasFocus()' "$shortcut"
grep -Fq 'Menu.IsVisible()' "$shortcut"
grep -Fq 'UnifiedPopup.IsVisible()' "$shortcut"
grep -Fq 'return "focused_text_field";' "$shortcut"
grep -Fq 'return "password_dialog";' "$shortcut"
grep -Fq 'return "menu";' "$shortcut"
grep -Fq 'new Entry("/", "Open Valheim' "$catalog"
grep -Fq 'new("/", "Open Valheim' "$catalog"

printf 'native console open-only shortcut checks passed\n'
