using NetTopologySuite.Geometries;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Entities.Emergency;

namespace RESQ.Infrastructure.Mappers.Emergency;

public static class SosRequestMapper
{
    public static SosRequest ToEntity(SosRequestModel model)
    {
        var entity = new SosRequest
        {
            ClusterId = model.ClusterId,
            UserId = model.UserId,
            RawMessage = model.RawMessage,
            PriorityLevel = model.PriorityLevel,
            Status = model.Status,
            WaitTimeMinutes = model.WaitTimeMinutes,
            CreatedAt = model.CreatedAt,
            LastUpdatedAt = model.LastUpdatedAt,
            ReviewedAt = model.ReviewedAt,
            ReviewedById = model.ReviewedById
        };

        if (model.Id > 0)
        {
            entity.Id = model.Id;
        }

        if (model.Location != null)
        {
            entity.Location = new Point(model.Location.Longitude, model.Location.Latitude) { SRID = 4326 };
        }

        return entity;
    }

    public static SosRequestModel ToDomain(SosRequest entity)
    {
        GeoLocation? location = null;
        if (entity.Location != null)
        {
            location = new GeoLocation(entity.Location.Y, entity.Location.X);
        }

        return new SosRequestModel
        {
            Id = entity.Id,
            ClusterId = entity.ClusterId,
            UserId = entity.UserId ?? Guid.Empty,
            Location = location,
            RawMessage = entity.RawMessage ?? string.Empty,
            PriorityLevel = entity.PriorityLevel,
            Status = entity.Status ?? string.Empty,
            WaitTimeMinutes = entity.WaitTimeMinutes,
            CreatedAt = entity.CreatedAt,
            LastUpdatedAt = entity.LastUpdatedAt,
            ReviewedAt = entity.ReviewedAt,
            ReviewedById = entity.ReviewedById
        };
    }
}