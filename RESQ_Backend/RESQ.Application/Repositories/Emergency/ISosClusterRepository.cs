using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Repositories.Emergency;

public interface ISosClusterRepository
{
    Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken cancellationToken = default);
    async Task<PagedResult<SosClusterModel>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        int? sosRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber <= 0 ? 1 : pageNumber;
        var normalizedPageSize = pageSize <= 0 ? 10 : pageSize;

        var filtered = (await GetAllAsync(cancellationToken))
            .Where(cluster => !sosRequestId.HasValue || cluster.SosRequestIds.Contains(sosRequestId.Value))
            .OrderByDescending(cluster => cluster.CreatedAt)
            .ThenByDescending(cluster => cluster.Id)
            .ToList();

        var items = filtered
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        return new PagedResult<SosClusterModel>(items, filtered.Count, normalizedPageNumber, normalizedPageSize);
    }

    Task<int> CreateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default);
    Task UpdateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default);
}
