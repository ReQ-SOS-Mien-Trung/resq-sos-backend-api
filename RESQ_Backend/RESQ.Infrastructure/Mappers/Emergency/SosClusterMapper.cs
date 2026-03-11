using NetTopologySuite.Geometries;
using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Entities.Emergency;

namespace RESQ.Infrastructure.Mappers.Emergency;

public static class SosClusterMapper
{
    public static SosCluster ToEntity(SosClusterModel model)
    {
        var entity = new SosCluster
        {
            RadiusKm = model.RadiusKm,
            SeverityLevel = model.SeverityLevel,
            WaterLevel = model.WaterLevel,
            VictimEstimated = model.VictimEstimated,
            ChildrenCount = model.ChildrenCount,
            ElderlyCount = model.ElderlyCount,
            MedicalUrgencyScore = model.MedicalUrgencyScore,
            CreatedAt = model.CreatedAt,
            LastUpdatedAt = model.LastUpdatedAt
        };

        if (model.CenterLatitude.HasValue && model.CenterLongitude.HasValue)
        {
            entity.CenterLocation = new Point(model.CenterLongitude.Value, model.CenterLatitude.Value) { SRID = 4326 };
        }

        return entity;
    }

    public static SosClusterModel ToDomain(SosCluster entity, IEnumerable<int>? sosRequestIds = null)
    {
        return new SosClusterModel
        {
            Id = entity.Id,
            CenterLatitude = entity.CenterLocation?.Y,
            CenterLongitude = entity.CenterLocation?.X,
            RadiusKm = entity.RadiusKm,
            SeverityLevel = entity.SeverityLevel,
            WaterLevel = entity.WaterLevel,
            VictimEstimated = entity.VictimEstimated,
            ChildrenCount = entity.ChildrenCount,
            ElderlyCount = entity.ElderlyCount,
            MedicalUrgencyScore = entity.MedicalUrgencyScore,
            CreatedAt = entity.CreatedAt,
            LastUpdatedAt = entity.LastUpdatedAt,
            IsMissionCreated = entity.IsMissionCreated,
            SosRequestIds = sosRequestIds?.ToList() ?? entity.SosRequests.Select(s => s.Id).ToList()
        };
    }
}
