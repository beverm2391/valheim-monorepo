using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.Interaction;

internal static class ComfortDiagnosticCommand
{
    internal static void Run(string[] arguments, Action<string> output)
    {
        if (arguments.Length != 0)
        {
            output("Usage: bhrun comfort");
            return;
        }

        if (!HealthReporting.GameplayActionsEnabled)
        {
            output(
                "The Benheim comfort diagnostic is unavailable because its required observation hooks did not load. Open Left Shift + B for details.");
            return;
        }

        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            output(
                "The Benheim comfort diagnostic is unavailable. Enter a playable world first.");
            return;
        }

        Emit(player, output);
    }

    private static void Emit(Player player, Action<string> output)
    {
        string operationId = Diagnostics.NewOperationId();
        ComfortDiagnosticSnapshot snapshot = ComfortDiagnosticCapture.Capture(player);
        int contributorCount = CountDecisions(
            snapshot.Candidates,
            ComfortDiagnosticDecision.Contributed,
            ComfortDiagnosticDecision.ContributedZero);
        IReadOnlyList<string> summary = ComfortDiagnosticSummary.Format(
            snapshot,
            contributorCount);

        Diagnostics.Emit(
            DiagnosticEvent.Create("Comfort", "comfort_debug_summary")
                .String("operation_id", operationId)
                .String("operation_phase", "snapshot")
                .Boolean("radius_used", snapshot.RadiusUsed)
                .Number("query_radius", snapshot.QueryRadius)
                .Number("patched_radius", ComfortFurnitureRangePatch.ExtendedComfortRadius)
                .Boolean("in_shelter", snapshot.InShelter)
                .Boolean("resting", snapshot.Resting)
                .Boolean("rested", snapshot.Rested)
                .Integer("calculated_comfort", snapshot.CalculatedComfort)
                .Integer("cached_comfort", snapshot.CachedComfort)
                .Integer("native_candidate_count", snapshot.Candidates.Count)
                .Integer("contributor_count", contributorCount)
                .Integer("duplicate_or_native_skip_count", snapshot.Candidates.Count - contributorCount)
                .Integer("native_candidate_reported", snapshot.Candidates.Count)
                .Boolean("native_candidate_truncated", false)
                .Integer("radius_exclusion_count", snapshot.RadiusExclusionCount)
                .Integer("radius_exclusion_reported", snapshot.RadiusExclusions.Count)
                .Boolean(
                    "radius_exclusion_truncated",
                    snapshot.RadiusExclusions.Count < snapshot.RadiusExclusionCount)
                .String("native_prefilter_visibility", "not_observable"));

        EmitPieces(
            operationId,
            "native_candidate",
            snapshot.Candidates,
            snapshot.Candidates.Count);
        EmitPieces(
            operationId,
            "nearest_radius_exclusion",
            snapshot.RadiusExclusions,
            snapshot.RadiusExclusions.Count);

        for (int index = 0; index < summary.Count; index++)
        {
            output(summary[index]);
        }
    }

    private static void EmitPieces(
        string operationId,
        string scope,
        IReadOnlyList<ComfortDiagnosticPiece> pieces,
        int count)
    {
        for (int index = 0; index < count; index++)
        {
            ComfortDiagnosticPiece piece = pieces[index];
            Diagnostics.Emit(
                DiagnosticEvent.Create("Comfort", "comfort_debug_piece")
                    .String("operation_id", operationId)
                    .String("operation_phase", "evidence")
                    .String("scope", scope)
                    .Integer("order", index)
                    .String("identity", piece.Identity)
                    .String("identity_scope", piece.IdentityScope)
                    .String("prefab", piece.Prefab)
                    .String("name_token", piece.NameToken)
                    .String("group", piece.Group)
                    .Number("distance", piece.Distance)
                    .Integer("comfort", piece.Comfort)
                    .String("decision", piece.Decision)
                    .Boolean("same_group_as_previous", piece.SameGroupAsPrevious)
                    .Boolean("same_name_as_previous", piece.SameNameAsPrevious));
        }
    }

    private static int CountDecisions(
        IReadOnlyList<ComfortDiagnosticPiece> pieces,
        string first,
        string second)
    {
        int count = 0;
        for (int index = 0; index < pieces.Count; index++)
        {
            string decision = pieces[index].Decision;
            if (decision == first || decision == second)
            {
                count++;
            }
        }
        return count;
    }
}
