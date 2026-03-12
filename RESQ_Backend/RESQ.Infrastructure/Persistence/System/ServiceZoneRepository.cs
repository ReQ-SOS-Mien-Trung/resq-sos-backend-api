using NetTopologySuite.Geometries;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Entities.System;
using RESQ.Infrastructure.Entities.System;
using RESQ.Infrastructure.Mappers.System;

namespace RESQ.Infrastructure.Persistence.System;

public class ServiceZoneRepository(IUnitOfWork unitOfWork) : IServiceZoneRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<ServiceZoneModel?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<ServiceZone>()
            .GetByPropertyAsync(x => x.IsActive, tracked: false);
        return entity == null ? null : ServiceZoneMapper.ToDomain(entity);
    }

    public async Task<ServiceZoneModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<ServiceZone>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);
        return entity == null ? null : ServiceZoneMapper.ToDomain(entity);
    }

    public async Task<List<ServiceZoneModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<ServiceZone>()
            .GetAllByPropertyAsync();
        return entities.Select(ServiceZoneMapper.ToDomain).ToList();
    }

    public async Task CreateAsync(ServiceZoneModel model, CancellationToken cancellationToken = default)
    {
        var entity = ServiceZoneMapper.ToEntity(model);
        await _unitOfWork.GetRepository<ServiceZone>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        model.Id = entity.Id;
    }

    public async Task UpdateAsync(ServiceZoneModel model, CancellationToken cancellationToken = default)
    {
        var entity = ServiceZoneMapper.ToEntity(model);
        await _unitOfWork.GetRepository<ServiceZone>().UpdateAsync(entity);
    }

    public async Task DeactivateAllExceptAsync(int excludeId, CancellationToken cancellationToken = default)
    {
        var others = await _unitOfWork.GetRepository<ServiceZone>()
            .GetAllByPropertyAsync(x => x.IsActive && x.Id != excludeId);

        foreach (var zone in others)
        {
            zone.IsActive = false;
            await _unitOfWork.GetRepository<ServiceZone>().UpdateAsync(zone);
        }

        if (others.Count > 0)
            await _unitOfWork.SaveAsync();
    }

    public async Task<bool> IsLocationInServiceZoneAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var zone = await GetActiveAsync(cancellationToken);
        if (zone == null || zone.Coordinates.Count < 3)
            return true; // Không có vùng → không giới hạn

        var factory = new GeometryFactory(new PrecisionModel(), 4326);

        // Đảm bảo polygon khép kín (điểm đầu = điểm cuối)
        var coords = zone.Coordinates
            .Select(c => new Coordinate(c.Longitude, c.Latitude))
            .ToList();

        if (!coords.First().Equals2D(coords.Last()))
            coords.Add(coords.First());

        var ring = factory.CreateLinearRing(coords.ToArray());
        var polygon = factory.CreatePolygon(ring);

        var point = factory.CreatePoint(new Coordinate(longitude, latitude));
        return polygon.Covers(point);
    }
}
