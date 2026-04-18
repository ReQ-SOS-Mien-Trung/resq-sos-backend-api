using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosClusters;

public class SosClusterDto
{
    public int Id { get; set; }
    public double? CenterLatitude { get; set; }
    public double? CenterLongitude { get; set; }
    public double? RadiusKm { get; set; }
    public string? SeverityLevel { get; set; }
    public string? WaterLevel { get; set; }
    public int? VictimEstimated { get; set; }
    public int? ChildrenCount { get; set; }
    public int? ElderlyCount { get; set; }
    public double? MedicalUrgencyScore { get; set; }
    public int SosRequestCount { get; set; }
    public List<int> SosRequestIds { get; set; } = [];
    public SosClusterStatus Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}
