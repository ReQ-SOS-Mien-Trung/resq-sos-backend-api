using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.Services;

public interface IStockThresholdResolver
{
    Task<StockThreshold> ResolveAsync(int depotId, int? categoryId, int itemModelId, CancellationToken cancellationToken = default);
    Task InvalidateDepotScopeAsync(int depotId);
}
