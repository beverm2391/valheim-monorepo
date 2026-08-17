using BenheimInventoryProtocol;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.InventoryFeature;

// Maps the shared protocol's complete typed model into the existing client
// event owner. Diagnostics.Emit keeps the readable line, local NDJSON, and
// optional direct-client Axiom copy on one event object.
internal sealed class InventoryTransactionDiagnosticSink : IInventoryTransactionDiagnosticSink
{
    internal static InventoryTransactionDiagnosticSink Instance { get; } =
        new InventoryTransactionDiagnosticSink();

    private InventoryTransactionDiagnosticSink()
    {
    }

    public void Emit(InventoryTransactionDiagnosticEvent source)
    {
        DiagnosticEvent target = DiagnosticEvent.Create(
            InventoryTransactionDiagnosticEvent.Domain,
            source.Name);
        foreach (InventoryTransactionDiagnosticField field in source.Fields)
        {
            switch (field.Kind)
            {
                case InventoryTransactionDiagnosticValueKind.String:
                    target.String(field.Name, field.Text);
                    break;
                case InventoryTransactionDiagnosticValueKind.Integer:
                    target.Integer(field.Name, field.Integer);
                    break;
                case InventoryTransactionDiagnosticValueKind.Number:
                    target.Number(field.Name, field.Number);
                    break;
                case InventoryTransactionDiagnosticValueKind.Boolean:
                    target.Boolean(field.Name, field.Boolean);
                    break;
            }
        }

        Diagnostics.Emit(target);
        if (source.Name == "client_refund_dropped")
        {
            TopLeftFeedbackHud.ShowTransient("Put Away refund dropped nearby. Pick it up.");
        }
    }
}
