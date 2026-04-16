using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Services;

public interface IStockThresholdResolver
{
    /// Resolve minimumThreshold theo cascade: DepotItem → DepotCategory → Depot → Global.
    /// Trả về (null, None) nếu không có scope nào configure threshold hợp lệ.
    /// </summary>
    Task<(int? Value, ThresholdResolutionScope Scope)> ResolveMinimumThresholdAsync(
        int depotId,
        int? categoryId,
        int itemModelId,
        CancellationToken cancellationToken = default);

    Task InvalidateDepotScopeAsync(int depotId);
    Task InvalidateGlobalAsync();
}
