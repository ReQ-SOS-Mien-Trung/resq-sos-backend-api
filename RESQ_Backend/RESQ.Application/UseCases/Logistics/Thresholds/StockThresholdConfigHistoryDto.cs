using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Thresholds;

public class StockThresholdConfigHistoryDto
{
    public int Id { get; set; }
    public int? ConfigId { get; set; }
    public StockThresholdScopeType ScopeType { get; set; }
    public int DepotId { get; set; }
    public int? CategoryId { get; set; }
    public int? ItemModelId { get; set; }
    public decimal? OldDangerRatio { get; set; }
    public decimal? OldWarningRatio { get; set; }
    public decimal? NewDangerRatio { get; set; }
    public decimal? NewWarningRatio { get; set; }
    public Guid ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? ChangeReason { get; set; }
    public string Action { get; set; } = string.Empty;
}
