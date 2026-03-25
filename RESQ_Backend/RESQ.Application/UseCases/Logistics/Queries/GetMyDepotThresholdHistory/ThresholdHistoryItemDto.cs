namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotThresholdHistory;

public class ThresholdHistoryItemDto
{
    public int Id { get; set; }
    public int? ConfigId { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public int DepotId { get; set; }
    public int? CategoryId { get; set; }
    public int? ItemModelId { get; set; }

    public decimal? OldDangerPercent { get; set; }
    public decimal? OldWarningPercent { get; set; }
    public decimal? NewDangerPercent { get; set; }
    public decimal? NewWarningPercent { get; set; }

    public Guid ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? ChangeReason { get; set; }
    public string Action { get; set; } = string.Empty;
}
