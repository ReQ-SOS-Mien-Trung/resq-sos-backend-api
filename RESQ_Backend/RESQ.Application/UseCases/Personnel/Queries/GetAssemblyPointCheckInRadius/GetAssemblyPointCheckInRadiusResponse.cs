namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointCheckInRadius;

public class GetAssemblyPointCheckInRadiusResponse
{
    /// <summary>ID điểm tập kết.</summary>
    public int AssemblyPointId { get; set; }

    /// <summary>Bán kính check-in hiện áp dụng (mét).</summary>
    public double MaxRadiusMeters { get; set; }

    /// <summary>
    /// true = đang dùng cấu hình toàn cục (điểm tập kết chưa có cấu hình riêng);
    /// false = đang dùng cấu hình riêng của điểm tập kết.
    /// </summary>
    public bool IsGlobalFallback { get; set; }

    /// <summary>Thời điểm cập nhật gần nhất (null nếu là cấu hình toàn cục).</summary>
    public DateTime? UpdatedAt { get; set; }
}
