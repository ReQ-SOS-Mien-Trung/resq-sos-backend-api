using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.Sorting;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Mappers.Emergency;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class SosRequestRepository(IUnitOfWork unitOfWork)
    : ISosRequestRepository, ISosRequestBulkReadRepository, ISosRequestMapReadRepository, ISosRequestStatisticsRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default)
    {
        var entity = SosRequestMapper.ToEntity(sosRequest);
        await _unitOfWork.GetRepository<SosRequest>().AddAsync(entity);
    }

    public async Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<SosRequest>();
        var existingEntity = await repository.GetByPropertyAsync(
            x => x.Id == sosRequest.Id,
            tracked: true
        );

        if (existingEntity != null)
        {
            SosRequestMapper.UpdateEntity(existingEntity, sosRequest);
            await repository.UpdateAsync(existingEntity);
        }
    }

    public async Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<SosRequest>()
            .GetAllByPropertyAsync(x => x.UserId == userId);

        return entities
            .OrderByDescending(x => x.CreatedAt)
            .Select(SosRequestMapper.ToDomain);
    }

    public async Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<SosRequest>()
            .GetAllByPropertyAsync(filter: null);

        return entities
            .OrderByDescending(x => x.CreatedAt)
            .Select(SosRequestMapper.ToDomain);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var rows = await _unitOfWork.GetRepository<SosRequest>()
            .AsQueryable(tracked: false)
            .Where(x =>
                x.ReceivedAt.HasValue
                && x.ReceivedAt.Value >= from
                && x.ReceivedAt.Value <= to
                && x.Status != null)
            .GroupBy(x => x.Status!)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Status, row => row.Count, StringComparer.Ordinal);
    }

    public async Task<List<SosRequestModel>> GetByBoundsAsync(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        IReadOnlyCollection<SosRequestStatus>? statuses = null,
        IReadOnlyCollection<SosPriorityLevel>? priorities = null,
        IReadOnlyCollection<SosRequestType>? sosTypes = null,
        IReadOnlyList<SosSortOption>? sortOptions = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.GetRepository<SosRequest>()
            .AsQueryable(tracked: false)
            .Where(x => x.Location != null);
        var statusNames = statuses?
            .Select(x => x.ToString())
            .Distinct()
            .ToArray();
        var priorityNames = priorities?
            .Select(x => x.ToString())
            .Distinct()
            .ToArray();
        var sosTypeNames = sosTypes?
            .Select(x => x.ToString())
            .Distinct()
            .ToArray();

        if (statusNames is { Length: > 0 })
        {
            query = query.Where(x => x.Status != null && statusNames.Contains(x.Status));
        }

        if (priorityNames is { Length: > 0 })
        {
            query = query.Where(x => x.PriorityLevel != null && priorityNames.Contains(x.PriorityLevel));
        }

        if (sosTypeNames is { Length: > 0 })
        {
            query = query.Where(x => x.SosType != null && sosTypeNames.Contains(x.SosType));
        }

        var entities = await query
            .ToListAsync(cancellationToken);

        var domainItems = entities
            .Where(x => IsInsideBounds(x, minLat, maxLat, minLng, maxLng))
            .Select(SosRequestMapper.ToDomain);

        return SosSortParser.ApplyToRequests(domainItems, sortOptions)
            .ToList();
    }

    private static bool IsInsideBounds(
        SosRequest request,
        double minLat,
        double maxLat,
        double minLng,
        double maxLng) =>
        request.Location is not null
        && request.Location.Y >= minLat
        && request.Location.Y <= maxLat
        && request.Location.X >= minLng
        && request.Location.X <= maxLng;

    public async Task<PagedResult<SosRequestModel>> GetAllPagedAsync(
        int pageNumber,
        int pageSize,
        IReadOnlyCollection<SosRequestStatus>? statuses = null,
        IReadOnlyCollection<SosPriorityLevel>? priorities = null,
        IReadOnlyCollection<SosRequestType>? sosTypes = null,
        IReadOnlyList<SosSortOption>? sortOptions = null,
        CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<SosRequest>();
        var statusNames = statuses?
            .Select(status => status.ToString())
            .Distinct()
            .ToArray();
        var priorityNames = priorities?
            .Select(priority => priority.ToString())
            .Distinct()
            .ToArray();
        var sosTypeNames = sosTypes?
            .Select(sosType => sosType.ToString())
            .Distinct()
            .ToArray();
        var hasStatusFilter = statusNames is { Length: > 0 };
        var hasPriorityFilter = priorityNames is { Length: > 0 };
        var hasSosTypeFilter = sosTypeNames is { Length: > 0 };

        var pagedEntities = await repository.GetPagedAsync(
            pageNumber,
            pageSize,
            filter: hasStatusFilter || hasPriorityFilter || hasSosTypeFilter
                ? request =>
                    (!hasStatusFilter || (request.Status != null && statusNames!.Contains(request.Status)))
                    && (!hasPriorityFilter || (request.PriorityLevel != null && priorityNames!.Contains(request.PriorityLevel)))
                    && (!hasSosTypeFilter || (request.SosType != null && sosTypeNames!.Contains(request.SosType)))
                : null,
            orderBy: q => ApplySort(q, sortOptions)
        );

        var domainItems = pagedEntities.Items
            .Select(SosRequestMapper.ToDomain)
            .ToList();

        return new PagedResult<SosRequestModel>(
            domainItems,
            pagedEntities.TotalCount,
            pagedEntities.PageNumber,
            pagedEntities.PageSize
        );
    }

    private static IOrderedQueryable<SosRequest> ApplySort(
        IQueryable<SosRequest> query,
        IReadOnlyList<SosSortOption>? sortOptions)
    {
        IOrderedQueryable<SosRequest>? ordered = null;

        foreach (var option in SosSortParser.Normalize(sortOptions))
        {
            ordered = option.Field switch
            {
                SosSortField.Time => ApplyTimeSort(query, ordered, option.Direction),
                SosSortField.Severity => ApplySeveritySort(query, ordered, option.Direction),
                _ => ordered
            };
        }

        return ApplyOrder(query, ordered, request => request.Id, descending: true);
    }

    private static IOrderedQueryable<SosRequest> ApplyTimeSort(
        IQueryable<SosRequest> query,
        IOrderedQueryable<SosRequest>? ordered,
        SosSortDirection direction)
    {
        ordered = ApplyOrder(query, ordered, request => request.CreatedAt == null, descending: false);
        return ApplyOrder(query, ordered, request => request.CreatedAt, direction == SosSortDirection.Desc);
    }

    private static IOrderedQueryable<SosRequest> ApplySeveritySort(
        IQueryable<SosRequest> query,
        IOrderedQueryable<SosRequest>? ordered,
        SosSortDirection direction)
    {
        ordered = ApplyOrder(query, ordered, RequestPriorityUnknownExpression, descending: false);
        return ApplyOrder(query, ordered, RequestPriorityRankExpression, direction == SosSortDirection.Desc);
    }

    private static Expression<Func<SosRequest, bool>> RequestPriorityUnknownExpression =>
        request => request.PriorityLevel == null
            || (request.PriorityLevel.ToLower() != "critical"
                && request.PriorityLevel.ToLower() != "high"
                && request.PriorityLevel.ToLower() != "medium"
                && request.PriorityLevel.ToLower() != "low");

    private static Expression<Func<SosRequest, int>> RequestPriorityRankExpression =>
        request => request.PriorityLevel != null && request.PriorityLevel.ToLower() == "critical" ? 4
            : request.PriorityLevel != null && request.PriorityLevel.ToLower() == "high" ? 3
            : request.PriorityLevel != null && request.PriorityLevel.ToLower() == "medium" ? 2
            : request.PriorityLevel != null && request.PriorityLevel.ToLower() == "low" ? 1
            : 0;

    private static IOrderedQueryable<SosRequest> ApplyOrder<TKey>(
        IQueryable<SosRequest> query,
        IOrderedQueryable<SosRequest>? ordered,
        Expression<Func<SosRequest, TKey>> keySelector,
        bool descending)
        => ordered is null
            ? descending
                ? query.OrderByDescending(keySelector)
                : query.OrderBy(keySelector)
            : descending
                ? ordered.ThenByDescending(keySelector)
                : ordered.ThenBy(keySelector);

    public async Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosRequest>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);

        return entity == null ? null : SosRequestMapper.ToDomain(entity);
    }

    public async Task<List<SosRequestModel>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (idList.Count == 0)
        {
            return new List<SosRequestModel>();
        }

        var entities = await _unitOfWork.GetRepository<SosRequest>()
            .AsQueryable(tracked: false)
            .Where(x => idList.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return entities
            .Select(SosRequestMapper.ToDomain)
            .ToList();
    }

    public async Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<SosRequest>()
            .GetAllByPropertyAsync(x => x.ClusterId == clusterId);

        return entities
            .OrderByDescending(x => x.CreatedAt)
            .Select(SosRequestMapper.ToDomain);
    }

    public async Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosRequest>()
            .GetByPropertyAsync(x => x.Id == id, tracked: true);

        if (entity is null) return;

        entity.Status = status.ToString();
        entity.LastUpdatedAt = DateTime.UtcNow;
        await _unitOfWork.GetRepository<SosRequest>().UpdateAsync(entity);
    }

    public async Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<SosRequest>()
            .GetAllByPropertyAsync(x => x.ClusterId == clusterId);

        var statusStr = status.ToString();
        foreach (var entity in entities)
        {
            entity.Status = statusStr;
            entity.LastUpdatedAt = DateTime.UtcNow;
            await _unitOfWork.GetRepository<SosRequest>().UpdateAsync(entity);
        }
    }

    public async Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var companionEntities = await _unitOfWork.GetRepository<SosRequestCompanion>()
            .GetAllByPropertyAsync(x => x.UserId == userId);

        var sosRequestIds = companionEntities.Select(c => c.SosRequestId).Distinct().ToList();
        if (sosRequestIds.Count == 0)
            return Enumerable.Empty<SosRequestModel>();

        var entities = await _unitOfWork.GetRepository<SosRequest>()
            .GetAllByPropertyAsync(x => sosRequestIds.Contains(x.Id));

        return entities
            .OrderByDescending(x => x.CreatedAt)
            .Select(SosRequestMapper.ToDomain);
    }
}
