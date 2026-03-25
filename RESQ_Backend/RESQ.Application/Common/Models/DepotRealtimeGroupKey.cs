namespace RESQ.Application.Common.Models;

public static class DepotRealtimeGroupKey
{
    public static string Build(int? missionId, int depotId)
    {
        var missionPart = missionId.HasValue ? missionId.Value.ToString() : "global";
        return $"mission:{missionPart}:depot:{depotId}";
    }
}
