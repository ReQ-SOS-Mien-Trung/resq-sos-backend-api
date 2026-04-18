using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class UpdateMyDepotThresholdRequest
{
    public StockThresholdScopeType ScopeType { get; set; }
    public int? CategoryId { get; set; }
    public int? ItemModelId { get; set; }
    /// <summary>
    /// Ngưỡng tối thiểu (số lượng). null = xóa cấu hình scope này (reset về scope cha).
    /// </summary>
    public int? MinimumThreshold { get; set; }

    public uint? RowVersion { get; set; }
    public string? Reason { get; set; }
}
