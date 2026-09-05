using TMPro;
using BenheimQoL.Shortcuts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BenheimQoL.Infrastructure;

internal static class InputState
{
    internal static bool IsShiftHeld()
    {
        if (IsTextEntryActive())
        {
            return false;
        }

        return Input.GetKey(KeyCode.LeftShift)
            || Input.GetKey(KeyCode.RightShift)
            || ZInput.GetKey(KeyCode.LeftShift)
            || ZInput.GetKey(KeyCode.RightShift);
    }

    internal static bool IsAltHeld()
    {
        if (IsTextEntryActive())
        {
            return false;
        }

        return Input.GetKey(KeyCode.LeftAlt)
            || Input.GetKey(KeyCode.RightAlt)
            || ZInput.GetKey(KeyCode.LeftAlt)
            || ZInput.GetKey(KeyCode.RightAlt);
    }

    internal static bool IsNativeRunHeld()
    {
        if (IsTextEntryActive())
        {
            return false;
        }

        return ZInput.GetButton("Run") || ZInput.GetButton("JoyRun");
    }

    internal static bool IsButtonDown(string name)
    {
        if (IsTextEntryActive())
        {
            return false;
        }

        return ZInput.GetButtonDown(name);
    }

    internal static bool IsKeyDown(KeyCode key)
    {
        if (IsTextEntryActive())
        {
            return false;
        }

        return Input.GetKeyDown(key) || ZInput.GetKeyDown(key);
    }

    internal static bool IsModifierHeld()
    {
        return Input.GetKey(KeyCode.LeftShift)
            || Input.GetKey(KeyCode.RightShift)
            || Input.GetKey(KeyCode.LeftAlt)
            || Input.GetKey(KeyCode.RightAlt)
            || Input.GetKey(KeyCode.LeftControl)
            || Input.GetKey(KeyCode.RightControl)
            || Input.GetKey(KeyCode.LeftMeta)
            || Input.GetKey(KeyCode.RightMeta)
            || ZInput.GetKey(KeyCode.LeftShift)
            || ZInput.GetKey(KeyCode.RightShift)
            || ZInput.GetKey(KeyCode.LeftAlt)
            || ZInput.GetKey(KeyCode.RightAlt)
            || ZInput.GetKey(KeyCode.LeftControl)
            || ZInput.GetKey(KeyCode.RightControl)
            || ZInput.GetKey(KeyCode.LeftMeta)
            || ZInput.GetKey(KeyCode.RightMeta);
    }

    internal static bool IsTextEntryActive()
    {
        if (ShortcutOverlay.IsOpen
            || Console.IsVisible()
            || Minimap.InTextInput()
            || TextInput.IsVisible())
        {
            return true;
        }

        TextInput textInput = TextInput.instance;
        if (textInput != null
            && textInput.m_panel != null
            && textInput.m_panel.activeInHierarchy)
        {
            return true;
        }

        GameObject? selected = EventSystem.current?.currentSelectedGameObject;
        if (selected == null)
        {
            return false;
        }

        TMP_InputField? tmpInput = selected.GetComponentInParent<TMP_InputField>();
        if (tmpInput != null && tmpInput.isFocused)
        {
            return true;
        }

        InputField? legacyInput = selected.GetComponentInParent<InputField>();
        return legacyInput != null && legacyInput.isFocused;
    }
}
