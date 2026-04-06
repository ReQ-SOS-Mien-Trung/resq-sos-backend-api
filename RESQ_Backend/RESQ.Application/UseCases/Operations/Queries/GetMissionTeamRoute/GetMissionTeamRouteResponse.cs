using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeamRoute;

public class GetMissionTeamRouteResponse
{
    public int MissionTeamId { get; set; }
    public int RescueTeamId { get; set; }
    public string? TeamName { get; set; }
    public string? TeamCode { get; set; }
    public string? MissionTeamStatus { get; set; }
    public string? RescueTeamStatus { get; set; }
    public double? TeamLatitude { get; set; }
    public double? TeamLongitude { get; set; }
    public DateTime? TeamLocationUpdatedAt { get; set; }
    public string? TeamLocationSource { get; set; }
    public double OriginLatitude { get; set; }
    public double OriginLongitude { get; set; }
    public string OriginSource { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int TotalDistanceMeters { get; set; }
    public int TotalDurationSeconds { get; set; }
    public string OverviewPolyline { get; set; } = string.Empty;
    /// <summary>Các điểm cần ghé, theo thứ tự Step của activity.</summary>
    public List<MissionRouteWaypoint> Waypoints { get; set; } = [];
    /// <summary>Legs[i] = đoạn từ Waypoints[i-1] đến Waypoints[i] (Legs[0] = origin → Waypoints[0]).</summary>
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
