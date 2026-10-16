using System;

namespace BenheimQoL.GreydwarfResident;

internal static class ResidentCapabilityNoticeTests
{
    internal static void Run()
    {
        var notice = new ResidentCapabilityNotice();

        // The first MissingSupport response matches ResidentClient's initial
        // readiness value, but must still become visible once Player exists.
        Expect(notice.ObserveCapability(changed: false, canRender: false),
            "first missing-support response is observable");
        Expect(!notice.TryTake(canDisplay: false), "early capability failure waits for Player and HUD");
        Expect(!notice.TryTake(canDisplay: false), "waiting does not consume the notice");
        Expect(notice.TryTake(canDisplay: true), "queued capability failure reaches Player");
        Expect(!notice.TryTake(canDisplay: true), "a single failure is shown once");

        notice.Reset();
        notice.Request(); // Production's no-response timeout path.
        Expect(!notice.TryTake(canDisplay: false), "timeout before Player is retained");
        Expect(notice.TryTake(canDisplay: true), "timeout is visible when Player appears");
        Expect(!notice.TryTake(canDisplay: true), "repeated timeout updates cannot spam");

        notice.Reset();
        notice.Request();
        Expect(!notice.TryTake(canDisplay: false), "pre-login failure remains queued");
        Expect(notice.ObserveCapability(changed: true, canRender: true),
            "recovery response is processed");
        Expect(!notice.TryTake(canDisplay: true), "recovery clears stale queued failure");

        notice.Reset();
        Expect(notice.ObserveCapability(changed: false, canRender: true),
            "first pending-join response is processed");
        Expect(!notice.TryTake(canDisplay: true), "pending joins do not produce failure notices");
        Expect(!notice.ObserveCapability(changed: false, canRender: true),
            "repeated pending responses are not state changes");
        Expect(!notice.TryTake(canDisplay: true), "pending-join updates remain quiet");

        notice.Reset();
        Expect(notice.ObserveCapability(changed: false, canRender: false),
            "new connection accepts its first failure");
        notice.Request();
        notice.Request();
        Expect(notice.TryTake(canDisplay: true), "repeated unavailable updates leave one notice");
        Expect(!notice.TryTake(canDisplay: true), "repeated unavailable updates do not duplicate");

        notice.ObserveCapability(changed: true, canRender: true);
        Expect(notice.ObserveCapability(changed: true, canRender: false),
            "a later failure after recovery is observable");
        Expect(notice.TryTake(canDisplay: true), "a later failure can be shown after recovery");

        Console.WriteLine("George capability notices cover first response, timeout, recovery, and pending joins");
    }

    private static void Expect(bool condition, string scenario)
    {
        if (!condition) throw new InvalidOperationException(scenario);
    }
}
