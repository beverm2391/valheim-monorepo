using BenheimQoL.Infrastructure;

namespace BenheimQoL.Production;

internal static class StationFillDiagnostics
{
    internal static string Started(
        string station,
        string input,
        float level,
        float capacity,
        string? item,
        string owner,
        long ownerPeer,
        bool zdoValid,
        long dataRevision)
    {
        string operationId = Diagnostics.NewOperationId();
        Diagnostics.Emit(
            DiagnosticEvent.Create("Production", "station_fill_started")
                .String("operation_id", operationId)
                .String("operation_phase", "start")
                .String("station", station)
                .String("input", input)
                .Number("level", level)
                .Number("capacity", capacity)
                .String("item", item ?? "auto")
                .String("owner", owner)
                .Integer("owner_peer", ownerPeer)
                .Boolean("zdo_valid", zdoValid)
                .Integer("data_revision", dataRevision));
        return operationId;
    }

    internal static void Finished(
        string operationId,
        string station,
        string input,
        int attempted,
        int accepted,
        string result,
        float level,
        float capacity,
        float elapsed,
        string owner,
        long ownerPeer,
        bool zdoValid,
        long dataRevision)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("Production", "station_fill_finished")
                .String("operation_id", operationId)
                .String("operation_phase", "terminal")
                .String("station", station)
                .String("input", input)
                .Integer("attempted", attempted)
                .Integer("accepted", accepted)
                .Integer("refunded", 0)
                .Integer("dropped", 0)
                .String("result", result)
                .Number("level", level)
                .Number("capacity", capacity)
                .Number("elapsed", elapsed)
                .String("owner", owner)
                .Integer("owner_peer", ownerPeer)
                .Boolean("zdo_valid", zdoValid)
                .Integer("data_revision", dataRevision));
    }
}
