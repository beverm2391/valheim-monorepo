using BenheimQoL.Infrastructure;

namespace BenheimQoL.GreydwarfResident;

internal static class ResidentDiagnostics
{
    internal static void Emit(string name, string reason, int resident = 0)
    {
        Diagnostics.Emit(DiagnosticEvent.Create("GreydwarfResident", name)
            .String("reason", reason).Integer("resident_instance", resident));
    }

    internal static void Operation(string name, string reason, string operationId, ZDOID tub, int generation)
    {
        Diagnostics.Emit(DiagnosticEvent.Create("GreydwarfResident", name)
            .String("reason", reason).String("operation_id", operationId)
            .String("tub_zdoid", tub.ToString()).Integer("generation", generation));
    }
}
