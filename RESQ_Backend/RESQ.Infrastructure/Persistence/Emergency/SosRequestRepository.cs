using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Mappers.Emergency;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class SosRequestRepository(IUnitOfWork unitOfWork) : ISosRequestRepository, ISosRequestBulkReadRepository
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

    public async Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<SosRequest>();

        var pagedEntities = await repository.GetPagedAsync(
            pageNumber,
            pageSize,
            filter: null,
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
