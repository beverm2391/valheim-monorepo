using System;
using BenheimQoL.GreydwarfResident;

// Timed player observations exercise the production visit state. The native
// movement, head animation, chair patches, and text display need a later
// installed-game pass; this proof does not pretend to render those systems.
ResidentVisit visit = new();
Observe(visit, 0, 9, false, ResidentReaction.None, "installation outside near radius", seed: true);
Observe(visit, 1, 4, false, ResidentReaction.Notice, "arrive");
Observe(visit, 2, 5, false, ResidentReaction.None, "cross near radius outward");
Observe(visit, 3, 4, false, ResidentReaction.None, "cross near radius inward without leaving visit");
Observe(visit, 4, 1, true, ResidentReaction.Acknowledge, "sit beside George");
Observe(visit, 5, 1, false, ResidentReaction.None, "stand up");
Observe(visit, 6, 1, true, ResidentReaction.None, "sit again without spamming acknowledgement");
Observe(visit, 7, 7, false, ResidentReaction.None, "start departure");
Observe(visit, 14.9f, 7, false, ResidentReaction.None, "not yet eight seconds away");
Observe(visit, 15, 4, false, ResidentReaction.None, "short departure returns before a reset observation");
Observe(visit, 16, 7, false, ResidentReaction.None, "start full departure");
Observe(visit, 24, 7, false, ResidentReaction.None, "complete eight seconds outside");
Observe(visit, 25, 4, false, ResidentReaction.Notice, "new visit after full departure");
Observe(visit, 26, 1, true, ResidentReaction.Acknowledge, "new visit permits new acknowledgement");

visit = new ResidentVisit();
Observe(visit, 0, 1, true, ResidentReaction.None, "initially soaking visitor is seeded", seed: true);
Observe(visit, 1, 1, false, ResidentReaction.None, "seeded visitor stands");
Observe(visit, 2, 1, true, ResidentReaction.None, "seeded visitor sits again");

visit = new ResidentVisit();
Observe(visit, 0, 4, false, ResidentReaction.None, "approach during cooldown is consumed", canNotice: false);
Observe(visit, 1, 4, false, ResidentReaction.None, "cooldown finishing cannot greet a lingering player");
Observe(visit, 2, 1, true, ResidentReaction.Acknowledge, "seating acknowledgement remains independent");

visit = new ResidentVisit();
Observe(visit, 0, 1, true, ResidentReaction.Acknowledge, "simultaneous entry and seating prioritizes acknowledgement");
Observe(visit, 1, 1, true, ResidentReaction.None, "held seated state does not repeat");

visit = new ResidentVisit();
Observe(visit, 0, 1, false, ResidentReaction.Notice, "visit before vertical departure");
Observe(visit, 1, 1, false, ResidentReaction.None, "visitor leaves vertically", vertical: -3.1f);
Observe(visit, 9, 1, false, ResidentReaction.None, "vertical departure completes", vertical: -3.1f);
Observe(visit, 10, 1, false, ResidentReaction.Notice, "return from vertical departure");
Expect(!ResidentVisit.IsOutside(6, 3), "exact wider boundary stays in visit");
Expect(ResidentVisit.IsOutside(6.01f, 0), "horizontal wider boundary");
Expect(ResidentVisit.IsOutside(0, -3.01f), "vertical distance uses absolute value");

Console.WriteLine("Greydwarf resident visit cadence and seating acknowledgement checks passed");

static void Observe(ResidentVisit visit, float time, float distance, bool seated,
    ResidentReaction expected, string scenario, bool seed = false, bool canNotice = true, float vertical = 0)
{
    ResidentReaction actual = visit.Observe(time, distance, vertical, seated, seed, canNotice);
    if (actual != expected) throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
}

static void Expect(bool condition, string scenario)
{
    if (!condition) throw new InvalidOperationException(scenario);
}
