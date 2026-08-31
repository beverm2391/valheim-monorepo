using System;
using BenheimQoL.WorldLabels;

internal static class Program
{
    private static void Main()
    {
        Assert(!WorldLabelVisibility.ShouldShowPortalTag("", true, 0f, true),
            "empty tags must stay hidden");
        Assert(!WorldLabelVisibility.ShouldShowPortalTag(null, true, 0f, true),
            "missing tags must stay hidden");
        Assert(!WorldLabelVisibility.ShouldShowPortalTag("home", false, 0f, true),
            "labels require a local viewer");
        Assert(!WorldLabelVisibility.ShouldShowPortalTag("home", true, 1f, false),
            "walls must hide labels");
        Assert(WorldLabelVisibility.ShouldShowPortalTag("home", true, 30f * 30f, true),
            "the 30-meter boundary must remain visible");
        Assert(!WorldLabelVisibility.ShouldShowPortalTag("home", true, 30.01f * 30.01f, true),
            "labels beyond 30 meters must stay hidden");
        Assert(WorldLabelVisibility.ShouldShowPortalTag("<b>exact</b>", true, 1f, true),
            "visibility policy must not interpret or rewrite tag text");

        Console.WriteLine("World Label behavior checks passed");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
