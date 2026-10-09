using BenheimQoL.Infrastructure;

namespace BenheimQoL.GreydwarfResident;

internal static class ResidentDiagnostics
{
    internal static void Emit(string name, string reason, int resident = 0)
    {
        Diagnostics.Emit(DiagnosticEvent.Create("GreydwarfResident", name)
            .String("reason", reason).Integer("resident_instance", resident));
    }
}
