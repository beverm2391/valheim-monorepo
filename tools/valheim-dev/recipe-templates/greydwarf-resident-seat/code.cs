using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

public static class ValheimDevChange
{
    static Harmony harmony;
    static GameObject resident;
    static Chair occupied;

    public static string Run(string json)
    {
        resident = GameObject.Find("Lab_GreydwarfResident");
        if (!resident) throw new Exception("Spawn the resident before reserving his seat.");
        occupied = resident.transform.parent.GetComponentsInChildren<Chair>(true)
            .Single(c => c.name == "SitPoint (3)");
        if (Vector3.Distance(occupied.m_attachPoint.position, resident.transform.position) > .6f)
            throw new Exception("Resident is no longer beside the expected tub seat.");
        // Native chairs reject IsInUse before attaching. Extend that one decision
        // for this cosmetic occupant; do not fabricate a Player or add physics.
        harmony = new Harmony("lab.greydwarf-resident.seat");
        harmony.Patch(AccessTools.Method(typeof(Chair), nameof(Chair.IsInUse)),
            postfix: new HarmonyMethod(typeof(ValheimDevChange), nameof(ReserveSeat)));
        harmony.Patch(AccessTools.Method(typeof(Chair), nameof(Chair.GetHoverText)),
            postfix: new HarmonyMethod(typeof(ValheimDevChange), nameof(HideSitPrompt)));
        return "{\"reservedSeat\":\"SitPoint (3)\",\"nativeInUse\":true,\"promptHidden\":true}";
    }

    public static void ReserveSeat(Chair __instance, ref bool __result)
    {
        if (resident && resident.activeInHierarchy && occupied && __instance == occupied)
            __result = true;
    }

    public static void HideSitPrompt(Chair __instance, ref string __result)
    {
        if (resident && resident.activeInHierarchy && occupied && __instance == occupied)
            __result = "";
    }

    public static void Cleanup()
    {
        if (harmony != null) harmony.UnpatchSelf();
        harmony = null;
        occupied = null;
        resident = null;
    }
}
