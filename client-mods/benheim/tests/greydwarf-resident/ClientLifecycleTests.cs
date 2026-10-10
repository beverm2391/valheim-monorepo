using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BenheimQoL.GreydwarfResident;

internal static class ClientLifecycleTests
{
    internal static void Run()
    {
        // Reproduce Johnny's ordering: the invited tub and compatible server
        // are present, but the local player is not ready. Later scans of the
        // same invitation must create George without a dismiss/reinvite.
        ResidentTubClient tub = NewTub();
        Tick(tub, 1);
        Tick(tub, 2);
        Expect(GreydwarfResidentRuntime.Attempts == 0 && !tub.Resident, "wait before native creation");
        Expect(ResidentDiagnostics.Events.Count(e => e.Event == "create_deferred") == 1, "one deferred event without scan spam");
        GreydwarfResidentRuntime.IsWorldReady = true;
        Tick(tub, 3);
        Expect(tub.Resident && GreydwarfResidentRuntime.Attempts == 1, "same invitation recovers after world readiness");
        Expect(ResidentDiagnostics.Events.Count(e => e.Event == "creation_resumed") == 1, "recovery is observable");
        Tick(tub, 4);
        Expect(GreydwarfResidentRuntime.Attempts == 1, "existing resident is not duplicated");

        tub = NewTub();
        Tick(tub, 1);
        tub.View.Data.Invited = false;
        GreydwarfResidentRuntime.IsWorldReady = true;
        Tick(tub, 2);
        Expect(!tub.Resident && GreydwarfResidentRuntime.Attempts == 0, "dismiss while loading cannot create a ghost");

        tub = NewTub();
        GreydwarfResidentRuntime.IsWorldReady = true;
        GreydwarfResidentRuntime.FailCreation = true;
        Tick(tub, 1);
        Tick(tub, 2);
        Expect(GreydwarfResidentRuntime.Attempts == 1, "real asset failure stays latched without repeated errors");
        GreydwarfResidentRuntime.FailCreation = false;
        tub.View.Data.Generation++;
        Tick(tub, 3);
        Expect(tub.Resident && GreydwarfResidentRuntime.Attempts == 2, "new invitation can recover from an asset failure");
        Console.WriteLine("George client late-join deferral, recovery, dismissal, and failure checks passed");
    }

    private static ResidentTubClient NewTub()
    {
        Time.time = 0;
        ResidentClient.Available = true;
        GreydwarfResidentRuntime.IsWorldReady = GreydwarfResidentRuntime.FailCreation = false;
        GreydwarfResidentRuntime.Attempts = 0;
        ResidentDiagnostics.Events.Clear();
        ResidentTubClient tub = new();
        tub.Configure(new Smelter(), new ZNetView());
        return tub;
    }

    private static void Tick(ResidentTubClient tub, float now)
    {
        Time.time = now;
        typeof(ResidentTubClient).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(tub, null);
    }

    private static void Expect(bool condition, string scenario)
    {
        if (!condition) throw new InvalidOperationException(scenario);
    }
}
