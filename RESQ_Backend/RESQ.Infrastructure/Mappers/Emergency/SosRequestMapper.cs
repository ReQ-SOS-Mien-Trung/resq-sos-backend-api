using NetTopologySuite.Geometries;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;
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
            SenderInfo = model.SenderInfo,
            VictimInfo = model.VictimInfo,
            IsSentOnBehalf = model.IsSentOnBehalf,
            OriginId = model.OriginId,
            PriorityLevel = model.PriorityLevel?.ToString(),
            Status = model.Status.ToString(),
            Timestamp = model.Timestamp,
            ReceivedAt = model.ReceivedAt,
            CreatedAt = model.CreatedAt,
            LastUpdatedAt = model.LastUpdatedAt,
            ReviewedAt = model.ReviewedAt,
            ReviewedById = model.ReviewedById,
            CreatedByCoordinatorId = model.CreatedByCoordinatorId
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
            SenderInfo = entity.SenderInfo,
            VictimInfo = entity.VictimInfo,
            IsSentOnBehalf = entity.IsSentOnBehalf,
            OriginId = entity.OriginId,
            PriorityLevel = Enum.TryParse<SosPriorityLevel>(entity.PriorityLevel, out var priority) ? priority : null,
            Status = Enum.TryParse<SosRequestStatus>(entity.Status, out var status) ? status : SosRequestStatus.Pending,
            Timestamp = entity.Timestamp,
            ReceivedAt = entity.ReceivedAt,
            CreatedAt = entity.CreatedAt,
            LastUpdatedAt = entity.LastUpdatedAt,
            ReviewedAt = entity.ReviewedAt,
            ReviewedById = entity.ReviewedById,
            CreatedByCoordinatorId = entity.CreatedByCoordinatorId
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
        entity.SenderInfo = model.SenderInfo;
        entity.VictimInfo = model.VictimInfo;
        entity.IsSentOnBehalf = model.IsSentOnBehalf;
        entity.OriginId = model.OriginId;
        entity.PriorityLevel = model.PriorityLevel?.ToString();
        entity.Status = model.Status.ToString();
        entity.Timestamp = model.Timestamp;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.ReviewedAt = model.ReviewedAt;
        entity.ReviewedById = model.ReviewedById;
        entity.CreatedByCoordinatorId = model.CreatedByCoordinatorId;

        if (model.Location != null)
        {
            entity.Location = new Point(model.Location.Longitude, model.Location.Latitude) { SRID = 4326 };
        }
    }
}