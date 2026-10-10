using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.Interaction;

// Chair has no initialization callback of its own. Piece.Awake runs after the
// placed prefab hierarchy and its serialized Chair fields are available, so
// configure only the seats belonging to the ordinary wooden bench prefab.
[HarmonyPatch(typeof(Piece), "Awake")]
internal static class WoodenBenchInteractionRangePatch
{
    private const string PrefabName = "piece_bench01";

    [HarmonyPostfix]
    private static void Postfix(Piece __instance)
    {
        if (!__instance || Utils.GetPrefabName(__instance.gameObject) != PrefabName)
        {
            return;
        }

        Chair[] seats = __instance.GetComponentsInChildren<Chair>(true);
        int adjusted = 0;
        int unchanged = 0;
        int missingAttachPoint = 0;
        foreach (Chair seat in seats)
        {
            if (!seat || !seat.m_attachPoint)
            {
                // Native Chair dereferences this anchor when checking distance
                // and sitting. Do not expand a seat that cannot satisfy that
                // native contract.
                missingAttachPoint++;
                continue;
            }

            float previous = seat.m_useDistance;
            if (previous < InteractionRanges.UseDistance)
            {
                seat.m_useDistance = InteractionRanges.UseDistance;
                adjusted++;
            }
            else
            {
                // Preserve any native or future prefab range beyond Benheim's
                // ordinary use radius.
                unchanged++;
            }
        }

        string result = seats.Length == 0
            ? "failed"
            : missingAttachPoint > 0 ? "partial" : "ready";
        string reason = seats.Length == 0
            ? "native_seats_missing"
            : missingAttachPoint > 0 ? "native_seat_anchor_missing" : "configured";
        Diagnostics.Emit(DiagnosticEvent.Create("Interaction", "wooden_bench_range_setup")
            .String("prefab", PrefabName)
            .String("result", result)
            .String("reason", reason)
            .Integer("seat_count", seats.Length)
            .Integer("adjusted_seat_count", adjusted)
            .Integer("unchanged_seat_count", unchanged)
            .Integer("missing_attach_point_count", missingAttachPoint)
            .Number("use_distance_m", InteractionRanges.UseDistance));
    }
}
