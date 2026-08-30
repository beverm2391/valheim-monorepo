using System.Collections.Generic;

namespace BenheimQoL.Interaction;

internal sealed class ComfortDiagnosticSnapshot
{
    internal bool RadiusUsed { get; set; }
    internal float QueryRadius { get; set; }
    internal bool InShelter { get; set; }
    internal bool Resting { get; set; }
    internal bool Rested { get; set; }
    internal int CalculatedComfort { get; set; }
    internal IReadOnlyList<ComfortDiagnosticPiece> Candidates { get; set; } =
        new List<ComfortDiagnosticPiece>();
    internal IReadOnlyList<ComfortDiagnosticPiece> RadiusExclusions { get; set; } =
        new List<ComfortDiagnosticPiece>();
    internal int RadiusExclusionCount { get; set; }
}

internal sealed class ComfortDiagnosticPiece
{
    internal ComfortDiagnosticPiece(
        string prefab,
        string nameToken,
        string group,
        float distance,
        int comfort,
        string decision)
    {
        Prefab = prefab;
        NameToken = nameToken;
        Group = group;
        Distance = distance;
        Comfort = comfort;
        Decision = decision;
    }

    internal string Prefab { get; }
    internal string NameToken { get; }
    internal string Group { get; }
    internal float Distance { get; }
    internal int Comfort { get; }
    internal string Decision { get; }
}
