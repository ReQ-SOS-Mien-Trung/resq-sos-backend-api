namespace RESQ.Application.UseCases.Emergency.Queries.GetRescueTeamsByCluster;

public class ClusterRescueTeamDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? TeamType { get; set; }
    public string? Status { get; set; }
    public int? MaxMembers { get; set; }

    /// <summary>Số thành viên đang Accepted trong đội.</summary>
    public int MemberCount { get; set; }

    public int? AssemblyPointId { get; set; }
    public string? AssemblyPointName { get; set; }
    public double? AssemblyPointLatitude { get; set; }
    public double? AssemblyPointLongitude { get; set; }

    /// <summary>
    /// Khoảng cách (km) từ điểm tập kết của đội tới tâm cluster SOS.
    /// Null nếu điểm tập kết chưa có tọa độ.
    /// </summary>
    public double? DistanceKm { get; set; }
}
