using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Farming;

internal static class FarmingInput
{
    internal static bool IsMassActionHeld()
    {
        return InputState.IsShiftHeld()
            || Input.GetKey(KeyCode.JoystickButton4)
            || ZInput.GetKey(KeyCode.JoystickButton4);
    }
}
