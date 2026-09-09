using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Interaction;

/// <summary>
/// Captures one explicitly requested native comfort calculation. The capture
/// observes Valheim's query result and final comfort additions; it never owns
/// or replaces the comfort calculation.
/// </summary>
internal static class ComfortDiagnosticCapture
{
    private const int MaximumRadiusExclusions = 8;
    private static CaptureState? active;

    internal static float ObserveRadius(float radius)
    {
        if (active != null)
        {
            active.QueryRadius = radius;
            active.RadiusUsed = true;
        }
        return radius;
    }

    internal static ComfortDiagnosticSnapshot Capture(Player player)
    {
        if (active != null)
        {
            throw new InvalidOperationException("A comfort diagnostic capture is already active.");
        }

        Vector3 position = player.transform.position;
        bool inShelter = player.InShelter();
        CaptureState state = new CaptureState();
        active = state;
        int calculatedComfort;
        try
        {
            calculatedComfort = SE_Rested.CalculateComfortLevel(player);
        }
        finally
        {
            active = null;
        }

        List<Piece> nativeCandidates = state.NativeCandidates == null
            ? new List<Piece>()
            : new List<Piece>(state.NativeCandidates);
        List<ComfortDiagnosticPiece> candidateRecords =
            CreateCandidateRecords(nativeCandidates, state.Contributions, position);

        List<Piece> allNativeComfortPieces = new List<Piece>();
        if (state.RadiusUsed)
        {
            Piece.GetAllComfortPiecesInRadius(position, float.MaxValue, allNativeComfortPieces);
        }
        HashSet<int> candidateIds = new HashSet<int>(
            nativeCandidates.Where(IsAvailable).Select(piece => piece.GetInstanceID()));

        List<ComfortDiagnosticPiece> radiusExclusions = allNativeComfortPieces
            .Where(IsAvailable)
            .Where(piece => !candidateIds.Contains(piece.GetInstanceID()))
            .OrderBy(piece => Vector3.Distance(position, piece.transform.position))
            .Take(MaximumRadiusExclusions)
            .Select(piece => CreatePiece(
                piece,
                position,
                ComfortDiagnosticDecision.RadiusExcluded))
            .ToList();

        return new ComfortDiagnosticSnapshot(
            state.RadiusUsed,
            state.QueryRadius,
            inShelter,
            player.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectResting),
            player.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectRested),
            calculatedComfort,
            player.GetComfortLevel(),
            candidateRecords,
            radiusExclusions,
            allNativeComfortPieces.Count(piece =>
                IsAvailable(piece) && !candidateIds.Contains(piece.GetInstanceID())));
    }

    internal static void ObserveCandidates(List<Piece> candidates)
    {
        if (active != null)
        {
            // Valheim sorts this same list after GetNearbyComfortPieces returns.
            // Keep the reference and copy it only after the native call completes.
            active.NativeCandidates = candidates;
        }
    }

    internal static void BeginNativeSort()
    {
        if (active != null)
        {
            active.SortDepth++;
        }
    }

    internal static void EndNativeSort()
    {
        if (active != null && active.SortDepth > 0)
        {
            active.SortDepth--;
        }
    }

    internal static void ObserveComfort(Piece piece, int comfort)
    {
        if (active != null && active.SortDepth == 0)
        {
            // Outside PieceComfortSort, this call is Valheim adding the piece's
            // comfort to the native total. Pieces skipped as duplicates never
            // reach this call.
            active.Contributions[piece.GetInstanceID()] = comfort;
        }
    }

    private static List<ComfortDiagnosticPiece> CreateCandidateRecords(
        List<Piece> candidates,
        IReadOnlyDictionary<int, int> contributions,
        Vector3 position)
    {
        List<ComfortDiagnosticPiece> records = new List<ComfortDiagnosticPiece>();
        for (int index = 0; index < candidates.Count; index++)
        {
            Piece piece = candidates[index];
            if (!IsAvailable(piece))
            {
                continue;
            }

            bool contributed = contributions.TryGetValue(piece.GetInstanceID(), out int comfort);
            if (!contributed)
            {
                comfort = piece.GetComfort();
            }
            Piece? previous = index > 0 && IsAvailable(candidates[index - 1])
                ? candidates[index - 1]
                : null;
            bool sameGroup = previous != null
                && piece.m_comfortGroup != Piece.ComfortGroup.None
                && piece.m_comfortGroup == previous.m_comfortGroup;
            bool sameName = previous != null && piece.m_name == previous.m_name;
            string decision = ComfortDiagnosticDecision.ForNativeCandidate(
                contributed,
                comfort,
                sameGroup,
                sameName);
            records.Add(CreatePiece(
                piece,
                position,
                decision,
                comfort,
                sameGroup,
                sameName));
        }
        return records;
    }

    private static ComfortDiagnosticPiece CreatePiece(
        Piece piece,
        Vector3 position,
        string decision,
        int? observedComfort = null,
        bool sameGroupAsPrevious = false,
        bool sameNameAsPrevious = false)
    {
        ZNetView? view = piece.GetComponent<ZNetView>();
        string prefab = Utils.GetPrefabName(piece.gameObject);
        bool hasWorldIdentity = view && view.IsValid();
        string identity = hasWorldIdentity
            ? $"{prefab}@zdo:{view!.GetZDO().m_uid}"
            : $"{prefab}@session_instance:{piece.GetInstanceID()}";
        return new ComfortDiagnosticPiece(
            identity,
            hasWorldIdentity ? "world_zdo" : "session_only",
            prefab,
            piece.m_name,
            piece.m_comfortGroup.ToString(),
            Vector3.Distance(position, piece.transform.position),
            observedComfort ?? piece.GetComfort(),
            decision,
            sameGroupAsPrevious,
            sameNameAsPrevious);
    }

    private static bool IsAvailable(Piece piece) => piece != null;

    private sealed class CaptureState
    {
        internal bool RadiusUsed;
        internal float QueryRadius;
        internal List<Piece>? NativeCandidates;
        internal int SortDepth;
        internal Dictionary<int, int> Contributions { get; } = new Dictionary<int, int>();
    }
}

internal sealed class ComfortDiagnosticSnapshot
{
    internal ComfortDiagnosticSnapshot(
        bool radiusUsed,
        float queryRadius,
        bool inShelter,
        bool resting,
        bool rested,
        int calculatedComfort,
        int cachedComfort,
        IReadOnlyList<ComfortDiagnosticPiece> candidates,
        IReadOnlyList<ComfortDiagnosticPiece> radiusExclusions,
        int radiusExclusionCount)
    {
        RadiusUsed = radiusUsed;
        QueryRadius = queryRadius;
        InShelter = inShelter;
        Resting = resting;
        Rested = rested;
        CalculatedComfort = calculatedComfort;
        CachedComfort = cachedComfort;
        Candidates = candidates;
        RadiusExclusions = radiusExclusions;
        RadiusExclusionCount = radiusExclusionCount;
    }

    internal bool RadiusUsed { get; }
    internal float QueryRadius { get; }
    internal bool InShelter { get; }
    internal bool Resting { get; }
    internal bool Rested { get; }
    internal int CalculatedComfort { get; }
    internal int CachedComfort { get; }
    internal IReadOnlyList<ComfortDiagnosticPiece> Candidates { get; }
    internal IReadOnlyList<ComfortDiagnosticPiece> RadiusExclusions { get; }
    internal int RadiusExclusionCount { get; }
}

internal sealed class ComfortDiagnosticPiece
{
    internal ComfortDiagnosticPiece(
        string identity,
        string identityScope,
        string prefab,
        string nameToken,
        string group,
        float distance,
        int comfort,
        string decision,
        bool sameGroupAsPrevious,
        bool sameNameAsPrevious)
    {
        Identity = identity;
        IdentityScope = identityScope;
        Prefab = prefab;
        NameToken = nameToken;
        Group = group;
        Distance = distance;
        Comfort = comfort;
        Decision = decision;
        SameGroupAsPrevious = sameGroupAsPrevious;
        SameNameAsPrevious = sameNameAsPrevious;
    }

    internal string Identity { get; }
    internal string IdentityScope { get; }
    internal string Prefab { get; }
    internal string NameToken { get; }
    internal string Group { get; }
    internal float Distance { get; }
    internal int Comfort { get; }
    internal string Decision { get; }
    internal bool SameGroupAsPrevious { get; }
    internal bool SameNameAsPrevious { get; }
}

[HarmonyPatch(typeof(SE_Rested), "GetNearbyComfortPieces")]
internal static class ComfortNativeCandidatesPatch
{
    [HarmonyPostfix]
    private static void Postfix(List<Piece> __result)
    {
        ComfortDiagnosticCapture.ObserveCandidates(__result);
    }
}

[HarmonyPatch(typeof(SE_Rested), "PieceComfortSort")]
internal static class ComfortNativeSortPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        ComfortDiagnosticCapture.BeginNativeSort();
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        ComfortDiagnosticCapture.EndNativeSort();
    }
}

[HarmonyPatch(typeof(Piece), nameof(Piece.GetComfort))]
internal static class ComfortNativeValuePatch
{
    [HarmonyPostfix]
    private static void Postfix(Piece __instance, int __result)
    {
        ComfortDiagnosticCapture.ObserveComfort(__instance, __result);
    }
}
