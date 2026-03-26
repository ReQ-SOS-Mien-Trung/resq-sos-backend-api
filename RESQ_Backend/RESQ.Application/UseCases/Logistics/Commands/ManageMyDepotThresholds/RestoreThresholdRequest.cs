namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class RestoreThresholdRequest
{
    /// <summary>ID của cấu hình ngưỡng không hoạt động (inactive) cần khôi phục.</summary>
    public int ConfigId { get; set; }

    /// <summary>Lý do khôi phục (tuỳ chọn).</summary>
    public string? Reason { get; set; }
}
