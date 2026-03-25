using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class ResetMyDepotThresholdRequest
{
    public StockThresholdScopeType ScopeType { get; set; }
    public int? CategoryId { get; set; }
    public int? ItemModelId { get; set; }
    public uint? RowVersion { get; set; }
    public string? Reason { get; set; }
}
