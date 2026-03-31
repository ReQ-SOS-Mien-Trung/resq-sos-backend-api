namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class StockThresholdCommandResponse
{
    public string ScopeType { get; set; } = string.Empty;
    public int? DepotId { get; set; }
    public int? CategoryId { get; set; }
    public int? ItemModelId { get; set; }
    public int? MinimumThreshold { get; set; }
    public uint? RowVersion { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
