namespace BenheimQoL.Interaction;

internal static class ComfortDiagnosticDecision
{
    internal const string Contributed = "contributed";
    internal const string ContributedZero = "contributed_zero";
    internal const string DuplicateGroup = "skipped_duplicate_group";
    internal const string DuplicateName = "skipped_duplicate_name";
    internal const string DuplicateGroupAndName = "skipped_duplicate_group_and_name";
    internal const string NativeSkipUnclassified = "skipped_native_unclassified";
    internal const string RadiusExcluded = "excluded_radius";

    internal static string ForNativeCandidate(
        bool contributionObserved,
        int comfort,
        bool sameGroupAsPrevious,
        bool sameNameAsPrevious)
    {
        if (contributionObserved)
        {
            return comfort == 0 ? ContributedZero : Contributed;
        }
        if (sameGroupAsPrevious && sameNameAsPrevious)
        {
            return DuplicateGroupAndName;
        }
        if (sameGroupAsPrevious)
        {
            return DuplicateGroup;
        }
        if (sameNameAsPrevious)
        {
            return DuplicateName;
        }
        return NativeSkipUnclassified;
    }
}
