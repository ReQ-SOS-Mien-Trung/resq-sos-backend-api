using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics;

/// <summary>
/// Repository quản lý bản ghi đóng kho (depot_closures).
/// </summary>
public interface IDepotClosureRepository
{
    Task<int> CreateAsync(DepotClosureRecord record, CancellationToken cancellationToken = default);

    Task<DepotClosureRecord?> GetByIdAsync(int closureId, CancellationToken cancellationToken = default);

    Task<DepotClosureRecord?> GetActiveClosureByDepotIdAsync(int depotId, CancellationToken cancellationToken = default);

    Task<DepotClosureRecord?> GetLatestClosureByDepotIdAsync(int depotId, CancellationToken cancellationToken = default);

    Task UpdateAsync(DepotClosureRecord record, CancellationToken cancellationToken = default);

    Task<bool> TryClaimForProcessingAsync(int closureId, CancellationToken cancellationToken = default);

    Task ResetProcessingToInProgressAsync(int closureId, CancellationToken cancellationToken = default);

    Task<bool> TryForceClaimFromProcessingAsync(int closureId, int expectedRowVersion, CancellationToken cancellationToken = default);

    Task UpdateProgressAsync(int closureId, int processedRows, int lastInventoryId,
        CancellationToken cancellationToken = default);

    Task<List<DepotClosureListItem>> GetClosuresByDepotIdAsync(int depotId, CancellationToken cancellationToken = default);

    Task<DepotClosureListItem?> GetClosureDetailAsync(int depotId, int closureId, CancellationToken cancellationToken = default);
}
