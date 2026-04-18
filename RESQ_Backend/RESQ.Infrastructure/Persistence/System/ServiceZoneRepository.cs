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

    public async Task<List<ServiceZoneModel>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<ServiceZone>()
            .GetAllByPropertyAsync(x => x.IsActive);
        return entities.Select(ServiceZoneMapper.ToDomain).ToList();
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
        var activeZones = await GetAllActiveAsync(cancellationToken);
        var validZones = activeZones.Where(z => z.Coordinates.Count >= 3).ToList();

        if (validZones.Count == 0)
            return true; // Không có vùng nào được cấu hình → không giới hạn

        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var point = factory.CreatePoint(new Coordinate(longitude, latitude));

        // Tọa độ nằm trong ít nhất một vùng active là được chấp nhận
        foreach (var zone in validZones)
        {
            var coords = zone.Coordinates
                .Select(c => new Coordinate(c.Longitude, c.Latitude))
                .ToList();

            if (!coords.First().Equals2D(coords.Last()))
                coords.Add(coords.First());

            var ring = factory.CreateLinearRing(coords.ToArray());
            var polygon = factory.CreatePolygon(ring);

            if (polygon.Covers(point))
                return true;
        }

        return false;
    }
}
