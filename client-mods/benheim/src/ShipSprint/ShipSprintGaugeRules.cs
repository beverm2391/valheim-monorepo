using System;
using System.Globalization;

namespace BenheimQoL.ShipSprint;

internal static class ShipSprintGaugeRules
{
    internal static float PlanarSpeed(float worldVelocityX, float worldVelocityZ)
    {
        return MathF.Sqrt(
            worldVelocityX * worldVelocityX
            + worldVelocityZ * worldVelocityZ);
    }

    internal static string Format(float metersPerSecond, bool sprintActive)
    {
        string speed = Math.Max(0f, metersPerSecond).ToString(
            "0.0",
            CultureInfo.InvariantCulture);
        return sprintActive
            ? $"{speed} m/s  <alpha=#A0>SPRINT</alpha>"
            : $"{speed} m/s";
    }
}
