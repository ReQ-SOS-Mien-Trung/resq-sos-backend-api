using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Mappers.Emergency;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class SosClusterRepository(IUnitOfWork unitOfWork) : ISosClusterRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosCluster>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false, includeProperties: "SosRequests");

        return entity is null ? null : SosClusterMapper.ToDomain(entity);
    }

    public async Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<SosCluster>()
            .GetAllByPropertyAsync(filter: null, includeProperties: "SosRequests");

        return entities
            .OrderByDescending(x => x.CreatedAt)
            .Select(entity => SosClusterMapper.ToDomain(entity));
    }

    public async Task<PagedResult<SosClusterModel>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        int? sosRequestId = null,
        IReadOnlyCollection<SosClusterStatus>? statuses = null,
        IReadOnlyCollection<SosPriorityLevel>? priorities = null,
        IReadOnlyCollection<SosRequestType>? sosTypes = null,
        CancellationToken cancellationToken = default)
    {
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
        var normalizedPageNumber = pageNumber <= 0 ? 1 : pageNumber;
        var normalizedPageSize = pageSize <= 0 ? 10 : pageSize;
        var hasStatusFilter = statusNames is { Length: > 0 };
        var hasPriorityFilter = priorityNames is { Length: > 0 };
        var hasSosTypeFilter = sosTypeNames is { Length: > 0 };
        var hasChildFilters = hasPriorityFilter || hasSosTypeFilter;

        IQueryable<SosCluster> query = _unitOfWork.GetRepository<SosCluster>()
            .AsQueryable(tracked: false)
            .Include(cluster => cluster.SosRequests);

        if (sosRequestId.HasValue)
        {
            var requestId = sosRequestId.Value;
            query = query.Where(cluster => cluster.SosRequests.Any(sosRequest => sosRequest.Id == requestId));
        }

        if (hasStatusFilter)
        {
            query = query.Where(cluster => statusNames!.Contains(cluster.Status));
        }

        if (hasChildFilters)
        {
            query = query.Where(cluster => cluster.SosRequests.Any(sosRequest =>
                (!hasPriorityFilter || (sosRequest.PriorityLevel != null && priorityNames!.Contains(sosRequest.PriorityLevel)))
                && (!hasSosTypeFilter || (sosRequest.SosType != null && sosTypeNames!.Contains(sosRequest.SosType)))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(cluster => cluster.CreatedAt)
            .ThenByDescending(cluster => cluster.Id)
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var models = entities
            .Select(entity => SosClusterMapper.ToDomain(entity))
            .ToList();

        return new PagedResult<SosClusterModel>(
            models,
            totalCount,
            normalizedPageNumber,
            normalizedPageSize);
    }

    public async Task<int> CreateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default)
    {
        var entity = SosClusterMapper.ToEntity(cluster);
        await _unitOfWork.GetRepository<SosCluster>().AddAsync(entity);
        await _unitOfWork.SaveAsync();

        // Assign cluster_id to all linked SOS requests
        if (cluster.SosRequestIds.Count > 0)
        {
            var sosRepo = _unitOfWork.GetRepository<SosRequest>();
            foreach (var sosId in cluster.SosRequestIds)
            {
                var sos = await sosRepo.GetByPropertyAsync(x => x.Id == sosId, tracked: true);
                if (sos is not null)
                {
                    sos.ClusterId = entity.Id;
                    await sosRepo.UpdateAsync(sos);
                }
            }
            await _unitOfWork.SaveAsync();
        }

        return entity.Id;
    }

    public async Task UpdateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosCluster>()
            .GetByPropertyAsync(x => x.Id == cluster.Id, tracked: true);

        if (entity is null) return;

        entity.RadiusKm = cluster.RadiusKm;
        entity.SeverityLevel = cluster.SeverityLevel;
        entity.WaterLevel = cluster.WaterLevel;
        entity.VictimEstimated = cluster.VictimEstimated;
        entity.ChildrenCount = cluster.ChildrenCount;
        entity.ElderlyCount = cluster.ElderlyCount;
        entity.MedicalUrgencyScore = cluster.MedicalUrgencyScore;
        entity.Status = cluster.Status.ToString();
        entity.LastUpdatedAt = DateTime.UtcNow;

        if (cluster.CenterLatitude.HasValue && cluster.CenterLongitude.HasValue)
        {
            entity.CenterLocation = new NetTopologySuite.Geometries.Point(
                cluster.CenterLongitude.Value, cluster.CenterLatitude.Value) { SRID = 4326 };
        }

        await _unitOfWork.GetRepository<SosCluster>().UpdateAsync(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<SosCluster>().DeleteAsync(id);
    }
}
