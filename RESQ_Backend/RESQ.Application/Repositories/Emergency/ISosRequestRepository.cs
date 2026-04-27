using RESQ.Application.Common.Models;
using RESQ.Application.Common.Sorting;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.Repositories.Emergency;

public interface ISosRequestRepository
{
    Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default);
    Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default);
    async Task<PagedResult<SosRequestModel>> GetAllPagedAsync(
        int pageNumber,
        int pageSize,
        IReadOnlyCollection<SosRequestStatus>? statuses = null,
        IReadOnlyCollection<SosPriorityLevel>? priorities = null,
        IReadOnlyCollection<SosRequestType>? sosTypes = null,
        IReadOnlyList<SosSortOption>? sortOptions = null,
        int? sosRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber <= 0 ? 1 : pageNumber;
        var normalizedPageSize = pageSize <= 0 ? 10 : pageSize;
        var statusSet = statuses?.ToHashSet();
        var prioritySet = priorities?.ToHashSet();
        var sosTypeSet = sosTypes?
            .Select(sosType => sosType.ToString())
            .ToHashSet(StringComparer.Ordinal);

        var filtered = (await GetAllAsync(cancellationToken))
            .Where(request => !sosRequestId.HasValue || (sosRequestId.Value > 0 && request.Id == sosRequestId.Value))
            .Where(request => statusSet is null || statusSet.Count == 0 || statusSet.Contains(request.Status))
            .Where(request => prioritySet is null || prioritySet.Count == 0 || (request.PriorityLevel.HasValue && prioritySet.Contains(request.PriorityLevel.Value)))
            .Where(request => sosTypeSet is null || sosTypeSet.Count == 0 || (!string.IsNullOrWhiteSpace(request.SosType) && sosTypeSet.Contains(request.SosType)))
            .ToList();

        var sorted = SosSortParser.ApplyToRequests(filtered, sortOptions)
            .ToList();

        var items = sorted
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        return new PagedResult<SosRequestModel>(items, filtered.Count, normalizedPageNumber, normalizedPageSize);
    }
    Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default);
    Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
