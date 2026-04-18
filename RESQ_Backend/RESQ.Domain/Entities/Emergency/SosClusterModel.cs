using RESQ.Domain.Enum.Emergency;

namespace RESQ.Domain.Entities.Emergency;

public class SosClusterModel
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
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public SosClusterStatus Status { get; set; } = SosClusterStatus.Pending;
    public List<int> SosRequestIds { get; set; } = [];
}
