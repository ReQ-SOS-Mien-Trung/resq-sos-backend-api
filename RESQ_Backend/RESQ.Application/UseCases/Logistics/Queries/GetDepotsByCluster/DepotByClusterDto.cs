namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotsByCluster;

/// <summary>Thông tin kho kèm khoảng cách tới cluster SOS.</summary>
public class DepotByClusterDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int CurrentUtilization { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>
    /// Khoảng cách (km) từ vị trí kho tới tâm cluster SOS.
    /// Null nếu kho chưa có tọa độ.
    /// </summary>
    public double? DistanceKm { get; set; }
}
