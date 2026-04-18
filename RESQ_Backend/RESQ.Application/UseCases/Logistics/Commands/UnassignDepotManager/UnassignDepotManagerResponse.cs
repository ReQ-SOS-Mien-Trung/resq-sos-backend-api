namespace RESQ.Application.UseCases.Logistics.Commands.UnassignDepotManager;

public class UnassignDepotManagerResponse
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UnassignedAt { get; set; }
    /// <summary>Danh sách userId đã được gỡ thành công.</summary>
    public List<Guid> UnassignedUserIds { get; set; } = [];
    /// <summary>Số manager còn lại đang active sau khi gỡ.</summary>
    public int RemainingManagerCount { get; set; }
}
