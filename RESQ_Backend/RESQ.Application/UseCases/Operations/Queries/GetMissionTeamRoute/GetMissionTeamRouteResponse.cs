using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeamRoute;

public class GetMissionTeamRouteResponse
{
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int TotalDistanceMeters { get; set; }
    public int TotalDurationSeconds { get; set; }
    public string OverviewPolyline { get; set; } = string.Empty;
    /// <summary>Các điểm cần ghé, theo thứ tự Step của activity.</summary>
    public List<MissionRouteWaypoint> Waypoints { get; set; } = [];
    /// <summary>
    /// Các chặng thực tế sau khi gộp waypoint trùng liên tiếp.
    /// Legs[0] = origin → điểm dừng đầu tiên; các phần tử sau là giữa hai điểm dừng liên tiếp.
    /// </summary>
    public List<GoongLegSummary> Legs { get; set; } = [];
}

public class MissionRouteWaypoint
{
    public int ActivityId { get; set; }
    public int? Step { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
