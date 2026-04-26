using RESQ.Application.Common.Models;
using RESQ.Application.Common.Sorting;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.Repositories.Emergency;

public interface ISosClusterRepository
{
    Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken cancellationToken = default);
    async Task<PagedResult<SosClusterModel>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        int? sosRequestId = null,
        IReadOnlyCollection<SosClusterStatus>? statuses = null,
        IReadOnlyCollection<SosPriorityLevel>? priorities = null,
        IReadOnlyCollection<SosRequestType>? sosTypes = null,
        IReadOnlyList<SosSortOption>? sortOptions = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber <= 0 ? 1 : pageNumber;
        var normalizedPageSize = pageSize <= 0 ? 10 : pageSize;
        var statusSet = statuses?.ToHashSet();

        var filtered = (await GetAllAsync(cancellationToken))
            .Where(cluster => !sosRequestId.HasValue || cluster.SosRequestIds.Contains(sosRequestId.Value))
            .Where(cluster => statusSet is null || statusSet.Count == 0 || statusSet.Contains(cluster.Status))
            .ToList();

        var sorted = SosSortParser.ApplyToClusters(filtered, sortOptions)
            .ToList();

        var items = sorted
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        return new PagedResult<SosClusterModel>(items, filtered.Count, normalizedPageNumber, normalizedPageSize);
    }

    Task<int> CreateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default);
    Task UpdateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
