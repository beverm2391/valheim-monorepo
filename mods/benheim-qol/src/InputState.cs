using UnityEngine;

namespace BenheimQoL;

internal static class InputState
{
    internal static bool IsShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    internal static bool IsAltHeld()
    {
        return Input.GetKey(KeyCode.LeftAlt)
            || Input.GetKey(KeyCode.RightAlt)
            || ZInput.GetKey(KeyCode.LeftAlt)
            || ZInput.GetKey(KeyCode.RightAlt);
    }

    internal static bool IsKeyDown(KeyCode key)
    {
        return Input.GetKeyDown(key) || ZInput.GetKeyDown(key);
    }
}
