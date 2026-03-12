namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryLogs;

public class InventoryLogDto
{
    public int Id { get; set; }
    public int? DepotSupplyInventoryId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string FormattedQuantityChange { get; set; } = string.Empty;
    public int? QuantityChange { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int? SourceId { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? PerformedByName { get; set; }
}