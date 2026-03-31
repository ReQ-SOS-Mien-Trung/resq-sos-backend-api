using RESQ.Application.UseCases.Logistics.Thresholds;

namespace RESQ.Application.Repositories.Logistics;

public interface IStockWarningBandConfigRepository
{
    Task<WarningBandConfigDto?> GetAsync(CancellationToken cancellationToken = default);

    Task<WarningBandConfigDto> UpsertAsync(
        List<WarningBandDto> bands,
        Guid updatedBy,
        CancellationToken cancellationToken = default);
}
