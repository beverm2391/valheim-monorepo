using UnityEngine;

namespace BenheimQoL;

internal static class InputState
{
    internal static bool IsShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
}
