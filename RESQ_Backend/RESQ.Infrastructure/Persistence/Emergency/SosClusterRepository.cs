using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
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
            .Select(e => SosClusterMapper.ToDomain(e));
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
        entity.IsMissionCreated = cluster.IsMissionCreated;
        entity.LastUpdatedAt = DateTime.UtcNow;

        if (cluster.CenterLatitude.HasValue && cluster.CenterLongitude.HasValue)
        {
            entity.CenterLocation = new NetTopologySuite.Geometries.Point(
                cluster.CenterLongitude.Value, cluster.CenterLatitude.Value) { SRID = 4326 };
        }

        await _unitOfWork.GetRepository<SosCluster>().UpdateAsync(entity);
    }
}
