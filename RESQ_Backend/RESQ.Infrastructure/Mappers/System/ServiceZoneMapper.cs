using System.Text.Json;
using RESQ.Domain.Entities.System;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Mappers.System;

public static class ServiceZoneMapper
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ServiceZoneModel ToDomain(ServiceZone entity)
    {
        List<CoordinatePoint> coords;
        try
        {
            coords = JsonSerializer.Deserialize<List<CoordinatePoint>>(entity.CoordinatesJson, _jsonOptions)
                     ?? new List<CoordinatePoint>();
        }
        catch
        {
            coords = new List<CoordinatePoint>();
        }

        return new ServiceZoneModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Coordinates = coords,
            IsActive = entity.IsActive,
            UpdatedBy = entity.UpdatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public static ServiceZone ToEntity(ServiceZoneModel model)
    {
        var entity = new ServiceZone
        {
            Name = model.Name,
            CoordinatesJson = JsonSerializer.Serialize(model.Coordinates, _jsonOptions),
            IsActive = model.IsActive,
            UpdatedBy = model.UpdatedBy,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };

        if (model.Id > 0)
            entity.Id = model.Id;

        return entity;
    }
}
