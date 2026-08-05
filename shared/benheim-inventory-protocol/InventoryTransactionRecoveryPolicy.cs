namespace BenheimInventoryProtocol;

internal enum PendingJournalPhase
{
    Prepared = 0,
    Reserved = 1,
    Completed = 2,
}

internal enum PendingJournalRecoveryAction
{
    None = 0,
    RestorePrepared = 1,
    ResumeReserved = 2,
    FinalizeCompleted = 3,
}

internal static class InventoryTransactionRecoveryPolicy
{
    internal const int CurrentProtocolVersion = 2;
    internal const int LegacyJournalProtocolVersion = 1;

    internal static bool CanReadRequest(int protocolVersion)
    {
        return protocolVersion == CurrentProtocolVersion
            || protocolVersion == LegacyJournalProtocolVersion;
    }

    internal static bool TryChooseAction(
        int protocolVersion,
        PendingJournalPhase phase,
        int requestedCount,
        int acceptedCount,
        out PendingJournalRecoveryAction action)
    {
        action = PendingJournalRecoveryAction.None;
        if (!CanReadRequest(protocolVersion) || requestedCount <= 0)
        {
            return false;
        }

        switch (phase)
        {
            case PendingJournalPhase.Prepared when acceptedCount == 0:
                action = PendingJournalRecoveryAction.RestorePrepared;
                return true;
            case PendingJournalPhase.Reserved when acceptedCount == 0:
                action = PendingJournalRecoveryAction.ResumeReserved;
                return true;
            case PendingJournalPhase.Completed when acceptedCount == requestedCount:
                action = PendingJournalRecoveryAction.FinalizeCompleted;
                return true;
            default:
                return false;
        }
    }
}
