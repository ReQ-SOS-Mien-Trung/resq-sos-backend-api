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
            CapacityTeams = model.CapacityTeams,
            Status = model.Status.ToString(),
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
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
        entity.CapacityTeams = model.CapacityTeams;
        entity.Status = model.Status.ToString();
        entity.UpdatedAt = model.UpdatedAt;

        if (model.Location != null)
        {
            entity.Location = new Point(model.Location.Longitude, model.Location.Latitude) { SRID = 4326 };
        }
    }

    public static AssemblyPointModel ToDomain(AssemblyPoint entity)
    {
        if (!Enum.TryParse<AssemblyPointStatus>(entity.Status, ignoreCase: true, out var status))
        {
            status = AssemblyPointStatus.Unavailable; 
        }

        GeoLocation? location = null;
        if (entity.Location != null)
        {
            // Instantiating the Personnel GeoLocation
            location = new GeoLocation(entity.Location.Y, entity.Location.X);
        }

        return new AssemblyPointModel
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Name = entity.Name ?? string.Empty,
            CapacityTeams = entity.CapacityTeams ?? 0,
            Status = status,
            Location = location,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
