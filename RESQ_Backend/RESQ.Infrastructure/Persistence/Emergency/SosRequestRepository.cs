using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Mappers.Emergency;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class SosRequestRepository(IUnitOfWork unitOfWork)
    : ISosRequestRepository, ISosRequestBulkReadRepository, ISosRequestMapReadRepository
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

    public async Task<List<SosRequestModel>> GetByBoundsAsync(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        IReadOnlyCollection<SosRequestStatus>? statuses = null,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.GetRepository<SosRequest>()
            .AsQueryable(tracked: false)
            .Where(x => x.Location != null);

        if (statuses is { Count: > 0 })
        {
            var statusNames = statuses
                .Select(x => x.ToString())
                .Distinct()
                .ToArray();

            query = query.Where(x => x.Status != null && statusNames.Contains(x.Status));
        }

        var entities = await query
            .ToListAsync(cancellationToken);

        return entities
            .Where(x => IsInsideBounds(x, minLat, maxLat, minLng, maxLng))
            .OrderByDescending(x => x.CreatedAt)
            .Select(SosRequestMapper.ToDomain)
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
        CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<SosRequest>();
        var statusNames = statuses?
            .Select(status => status.ToString())
            .Distinct()
            .ToArray();

        var pagedEntities = await repository.GetPagedAsync(
            pageNumber,
            pageSize,
            filter: statusNames is { Length: > 0 }
                ? request => request.Status != null && statusNames.Contains(request.Status)
                : null,
            orderBy: q => q.OrderByDescending(x => x.CreatedAt)
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
