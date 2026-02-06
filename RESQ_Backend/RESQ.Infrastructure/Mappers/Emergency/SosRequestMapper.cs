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
            PacketId = model.PacketId,
            ClusterId = model.ClusterId,
            UserId = model.UserId,
            LocationAccuracy = model.LocationAccuracy,
            SosType = model.SosType,
            RawMessage = model.RawMessage,
            StructuredData = model.StructuredData,
            NetworkMetadata = model.NetworkMetadata,
            PriorityLevel = model.PriorityLevel,
            Status = model.Status,
            WaitTimeMinutes = model.WaitTimeMinutes,
            Timestamp = model.Timestamp,
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
            PacketId = entity.PacketId,
            ClusterId = entity.ClusterId,
            UserId = entity.UserId ?? Guid.Empty,
            Location = location,
            LocationAccuracy = entity.LocationAccuracy,
            SosType = entity.SosType,
            RawMessage = entity.RawMessage ?? string.Empty,
            StructuredData = entity.StructuredData,
            NetworkMetadata = entity.NetworkMetadata,
            PriorityLevel = entity.PriorityLevel,
            Status = entity.Status ?? string.Empty,
            WaitTimeMinutes = entity.WaitTimeMinutes,
            Timestamp = entity.Timestamp,
            CreatedAt = entity.CreatedAt,
            LastUpdatedAt = entity.LastUpdatedAt,
            ReviewedAt = entity.ReviewedAt,
            ReviewedById = entity.ReviewedById
        };
    }

    public static void UpdateEntity(SosRequest entity, SosRequestModel model)
    {
        entity.PacketId = model.PacketId;
        entity.ClusterId = model.ClusterId;
        entity.LocationAccuracy = model.LocationAccuracy;
        entity.SosType = model.SosType;
        entity.RawMessage = model.RawMessage;
        entity.StructuredData = model.StructuredData;
        entity.NetworkMetadata = model.NetworkMetadata;
        entity.PriorityLevel = model.PriorityLevel;
        entity.Status = model.Status;
        entity.WaitTimeMinutes = model.WaitTimeMinutes;
        entity.Timestamp = model.Timestamp;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.ReviewedAt = model.ReviewedAt;
        entity.ReviewedById = model.ReviewedById;

        if (model.Location != null)
        {
            entity.Location = new Point(model.Location.Longitude, model.Location.Latitude) { SRID = 4326 };
        }
    }
}