using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.Interaction;

internal static class ComfortDiagnosticCommand
{
    internal const string Usage = "bh debug comfort";

    internal static bool TryExecute(string[] arguments, Terminal context)
    {
        if (!HasPrefix(arguments))
        {
            return false;
        }
        if (arguments.Length != 3)
        {
            PrintUsage(context);
            return true;
        }

        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            context.AddString(
                "The Benheim comfort diagnostic is unavailable. Enter a playable world first.");
            return true;
        }

        try
        {
            Emit(player, context);
        }
        catch (Exception exception)
        {
            context.AddString(
                $"Benheim comfort diagnostic failed: {Diagnostics.Flatten(exception.Message)}");
        }
        return true;
    }

    internal static void PrintUsage(Terminal context)
    {
        context.AddString($"  {Usage}");
        context.AddString(
            "  record one Valheim comfort calculation in the diagnostic log, including why Valheim counted or skipped each recorded piece of comfort furniture");
    }

    private static void Emit(Player player, Terminal context)
    {
        string operationId = Diagnostics.NewOperationId();
        ComfortDiagnosticSnapshot snapshot = ComfortDiagnosticCapture.Capture(player);
        int contributorCount = CountDecisions(
            snapshot.Candidates,
            ComfortDiagnosticDecision.Contributed,
            ComfortDiagnosticDecision.ContributedZero);

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

        string radius = snapshot.RadiusUsed
            ? $"{snapshot.QueryRadius:0.##}m"
            : "not used because the player is not sheltered";
        context.AddString(
            $"Benheim wrote the comfort diagnostic to the diagnostic log. Radius: {radius}. " +
            $"Calculated comfort: {snapshot.CalculatedComfort}. " +
            $"Cached comfort: {snapshot.CachedComfort}. " +
            $"Furniture candidates: {snapshot.Candidates.Count}.");
        context.AddString(
            $"Benheim recorded all {snapshot.Candidates.Count} native comfort candidates and " +
            $"{snapshot.RadiusExclusions.Count} of {snapshot.RadiusExclusionCount} nearest radius exclusions.");
        context.AddString(
            "Pieces that Valheim hides before its native comfort query are not observable by this command.");
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

    private static bool HasPrefix(string[] arguments)
    {
        return arguments.Length >= 3
            && string.Equals(arguments[0], "bh", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[1], "debug", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[2], "comfort", StringComparison.OrdinalIgnoreCase);
    }
}
