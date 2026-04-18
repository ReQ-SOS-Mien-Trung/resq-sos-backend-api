namespace RESQ.Application.UseCases.Personnel.Queries.GetRescueTeamsByCluster;

/// <summary>Thông tin đội cứu hộ kèm khoảng cách tới cluster SOS.</summary>
public class RescueTeamByClusterDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TeamType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AssemblyPointId { get; set; }
    public string? AssemblyPointName { get; set; }

    /// <summary>Khoảng cách (km) từ điểm tập kết của đội tới trung tâm cluster. Null nếu không xác định được toạ độ.</summary>
    public double? DistanceKm { get; set; }

    public int MaxMembers { get; set; }
    public int CurrentMemberCount { get; set; }
}
