using System;
using BenheimQoL.GreydwarfResident;

internal static class DrowsinessTests
{
    internal static void Run()
    {
        ResidentDrowsiness rest = new();
        rest.Reset(0);
        rest.Update(5.99f, 1, night: true, quiet: true);
        Expect(!rest.IsDrowsy && rest.Weight == 0, "new resident settles before dozing");
        rest.Update(6, 4, night: true, quiet: true);
        Expect(rest.IsDrowsy && rest.Weight == 1, "quiet native night reaches drowsy pose");

        // Approach wake and visit debounce cooperate: staying nearby cannot
        // produce another greeting when the wake hold expires.
        ResidentVisit visit = new();
        Expect(visit.Observe(7, 4, 0, false, false, true) == ResidentReaction.Notice,
            "approach can wake a drowsy resident");
        Expect(rest.Wake(7), "visitor wake reports interrupted drowsiness");
        rest.Update(8, 1, night: true, quiet: false);
        Expect(!rest.IsDrowsy && rest.Weight == 0, "visitor reaction clears the head droop");
        rest.Update(26.99f, 1, night: true, quiet: true);
        Expect(!rest.IsDrowsy, "visitor wake holds for twenty seconds");
        rest.Update(27, 4, night: true, quiet: true);
        Expect(rest.IsDrowsy, "quiet lingering visit can return to drowsiness");
        Expect(visit.Observe(27, 4, 0, false, false, true) == ResidentReaction.None,
            "returning to drowsiness does not reset the greeting debounce");

        rest.Update(28, 1, night: false, quiet: true);
        Expect(!rest.IsDrowsy && rest.Weight == 0, "daytime clears drowsiness without a visitor");
        rest.Update(30, 4, night: true, quiet: true);
        Expect(rest.IsDrowsy, "a later night may doze again");
        Expect(rest.Wake(31), "shared speech wakes a client without a local approach");
        rest.Update(52, 4, night: true, quiet: false);
        Expect(!rest.IsDrowsy && rest.Weight == 0, "active speech outranks the expired wake deadline");
        rest.Update(53, 4, night: true, quiet: true);
        Expect(rest.IsDrowsy, "speech ending allows quiet nighttime rest");
        rest.Reset(60);
        Expect(!rest.IsDrowsy && rest.Weight == 0, "re-enabled resident starts with an upright pose");
    }

    private static void Expect(bool condition, string scenario)
    {
        if (!condition) throw new InvalidOperationException(scenario);
    }
}
