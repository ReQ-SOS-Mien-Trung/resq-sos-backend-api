using NetTopologySuite.Geometries;
using RESQ.Domain.Entities.Personnel.ValueObjects;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Personnel;
using RESQ.Infrastructure.Entities.Personnel;

namespace RESQ.Infrastructure.Mappers.Personnel;

public static class AssemblyPointMapper
{
    public static AssemblyPoint ToEntity(AssemblyPointModel model)
    {
        var entity = new AssemblyPoint
        {
            Code = model.Code,
            Name = model.Name,
            MaxCapacity = model.MaxCapacity,
            Status = model.Status.ToString(),
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            ImageUrl = model.ImageUrl,
            StatusReason = model.StatusReason,
            StatusChangedAt = model.StatusChangedAt,
            StatusChangedBy = model.StatusChangedBy
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

    public static void UpdateEntity(AssemblyPoint entity, AssemblyPointModel model)
    {
        entity.Code = model.Code;
        entity.Name = model.Name;
        entity.MaxCapacity = model.MaxCapacity;
        entity.Status = model.Status.ToString();
        entity.UpdatedAt = model.UpdatedAt;
        entity.ImageUrl = model.ImageUrl;
        entity.StatusReason = model.StatusReason;
        entity.StatusChangedAt = model.StatusChangedAt;
        entity.StatusChangedBy = model.StatusChangedBy;

        if (model.Location != null)
        {
            entity.Location = new Point(model.Location.Longitude, model.Location.Latitude) { SRID = 4326 };
        }
    }

    public static AssemblyPointModel ToDomain(AssemblyPoint entity)
    {
        // Fallback về Created nếu DB chứa giá trị không hợp lệ (e.g. "Active" từ migration cũ)
        if (!Enum.TryParse<AssemblyPointStatus>(entity.Status, ignoreCase: true, out var status))
        {
            status = AssemblyPointStatus.Created;
        }

        GeoLocation? location = null;
        if (entity.Location != null)
        {
            // PostGIS lưu X=Longitude, Y=Latitude
            location = new GeoLocation(entity.Location.Y, entity.Location.X);
        }

        return new AssemblyPointModel
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Name = entity.Name ?? string.Empty,
            MaxCapacity = entity.MaxCapacity ?? 0,
            Status = status,
            Location = location,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            ImageUrl = entity.ImageUrl,
            StatusReason = entity.StatusReason,
            StatusChangedAt = entity.StatusChangedAt,
            StatusChangedBy = entity.StatusChangedBy
        };
    }
}
