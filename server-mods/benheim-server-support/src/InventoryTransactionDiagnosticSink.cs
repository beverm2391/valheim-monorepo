using BenheimInventoryProtocol;
using BepInEx.Logging;

namespace BenheimServerSupport;

// The dedicated server has no client NDJSON or private Axiom sink. It renders
// the same typed protocol event as a readable line in the server log.
internal sealed class InventoryTransactionDiagnosticSink : IInventoryTransactionDiagnosticSink
{
    private readonly ManualLogSource log;

    internal InventoryTransactionDiagnosticSink(ManualLogSource log)
    {
        this.log = log;
    }

    public void Emit(InventoryTransactionDiagnosticEvent diagnosticEvent)
    {
        string line = diagnosticEvent.ToReadableLine();
        if (diagnosticEvent.Level == InventoryTransactionDiagnosticLevel.Warning)
        {
            log.LogWarning(line);
        }
        else
        {
            log.LogInfo(line);
        }
    }
}
