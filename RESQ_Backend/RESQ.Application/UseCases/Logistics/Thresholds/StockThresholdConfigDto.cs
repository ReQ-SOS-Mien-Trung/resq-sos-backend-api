using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Thresholds;

public class StockThresholdConfigDto
{
    public int Id { get; set; }
    public StockThresholdScopeType ScopeType { get; set; }
    public int? DepotId { get; set; }
    public int? CategoryId { get; set; }
    public int? ItemModelId { get; set; }
    public int? MinimumThreshold { get; set; }
    public bool IsActive { get; set; }
    public uint RowVersion { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
