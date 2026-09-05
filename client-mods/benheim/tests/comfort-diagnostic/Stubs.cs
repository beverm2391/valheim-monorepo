using System.Collections.Generic;

internal sealed class Player
{
    internal static Player? m_localPlayer;
}

namespace BenheimQoL.Infrastructure
{
    internal static class HealthReporting
    {
        internal static bool GameplayActionsEnabled { get; set; } = true;
    }

    internal sealed class DiagnosticEvent
    {
        private DiagnosticEvent(string domain, string name)
        {
            Domain = domain;
            Name = name;
        }

        internal string Domain { get; }
        internal string Name { get; }
        internal Dictionary<string, object?> Fields { get; } = new();

        internal static DiagnosticEvent Create(string domain, string name) =>
            new(domain, name);

        internal DiagnosticEvent String(string name, string? value) =>
            Add(name, value);

        internal DiagnosticEvent Integer(string name, int value) =>
            Add(name, value);

        internal DiagnosticEvent Number(string name, float value) =>
            Add(name, value);

        internal DiagnosticEvent Boolean(string name, bool value) =>
            Add(name, value);

        private DiagnosticEvent Add(string name, object? value)
        {
            Fields.Add(name, value);
            return this;
        }
    }

    internal static class Diagnostics
    {
        internal static List<DiagnosticEvent> Events { get; } = new();

        internal static string NewOperationId() => "comfort-operation";

        internal static void Emit(DiagnosticEvent diagnosticEvent) =>
            Events.Add(diagnosticEvent);
    }
}

namespace BenheimQoL.Interaction
{
    internal static class ComfortFurnitureRangePatch
    {
        internal const float ExtendedComfortRadius = 20f;
    }

    internal static class ComfortDiagnosticCapture
    {
        internal static ComfortDiagnosticSnapshot Snapshot { get; set; } = null!;

        internal static ComfortDiagnosticSnapshot Capture(Player player) => Snapshot;
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
}
