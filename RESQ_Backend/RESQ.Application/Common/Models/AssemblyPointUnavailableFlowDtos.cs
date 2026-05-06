using System.Text.Json.Serialization;

namespace RESQ.Application.Common.Models;

public static class AssemblyPointUnavailableImpactReason
{
    public const string HasMissionActivityTargetingUnavailablePoint = nameof(HasMissionActivityTargetingUnavailablePoint);
}

public sealed class AssemblyPointUnavailableAlternativeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MaxCapacity { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? DistanceKm { get; set; }
}

public sealed class AssemblyPointUnavailableTeamlessRescuerDto
{
    public Guid UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public string? RescuerType { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public int AssemblyEventId { get; set; }
    public List<string> TopAbilities { get; set; } = [];
}

public sealed class AssemblyPointUnavailableMissionActivityDto
{
    public int MissionActivityId { get; set; }
    public int? MissionId { get; set; }
    public int? MissionTeamId { get; set; }
    public int? RescueTeamId { get; set; }
    public string? RescueTeamCode { get; set; }
    public string? RescueTeamName { get; set; }
    public int? Step { get; set; }
    public string? ActivityType { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class AssemblyPointUnavailableRescueTeamImpactDto
{
    public int? RescueTeamId { get; set; }
    public string? RescueTeamCode { get; set; }
    public string? RescueTeamName { get; set; }
    public string? RescueTeamStatus { get; set; }
    public int? MissionTeamId { get; set; }
    [JsonPropertyName("impactReason")]
    public List<string> ImpactReasons { get; set; } = [];
    public List<Guid> MemberUserIds { get; set; } = [];
    public List<AssemblyPointUnavailableMissionActivityDto> Activities { get; set; } = [];
}

public sealed class AssemblyPointUnavailableStationedTeamDto
{
    public int RescueTeamId { get; set; }
    public string? RescueTeamCode { get; set; }
    public string? RescueTeamName { get; set; }
    public string? RescueTeamStatus { get; set; }
    public List<Guid> MemberUserIds { get; set; } = [];
}

public sealed class AssemblyPointUnavailableImpactResponse
{
    public int AssemblyPointId { get; set; }
    public string AssemblyPointCode { get; set; } = string.Empty;
    public string AssemblyPointName { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public DateTime? StatusChangedAt { get; set; }
    public List<AssemblyPointUnavailableAlternativeDto> AvailableAssemblyPoints { get; set; } = [];
    public List<AssemblyPointUnavailableRescueTeamImpactDto> RescueTeams { get; set; } = [];
    public List<AssemblyPointUnavailableStationedTeamDto> StationedTeams { get; set; } = [];
    public List<AssemblyPointUnavailableTeamlessRescuerDto> TeamlessCheckedInRescuers { get; set; } = [];
    public List<AssemblyPointUnavailableTeamlessRescuerDto> CheckedInRescuers { get; set; } = [];
}
