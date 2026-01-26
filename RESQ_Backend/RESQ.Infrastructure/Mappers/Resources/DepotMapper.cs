using RESQ.Application.UseCases.Resources.Queries.Depot;
using RESQ.Domain.Entities.Resources;
using RESQ.Domain.Entities.Resources.Exceptions;
using RESQ.Domain.Enum.Resources;
using RESQ.Infrastructure.Entities;

namespace RESQ.Infrastructure.Mappers.Resources
{
    public class DepotMapper
    {
        public static Depot ToEntity(DepotModel model)
        {
            var entity = new Depot
            {
                Name = model.Name,
                Address = model.Address,
                Capacity = model.Capacity,
                CurrentUtilization = model.CurrentUtilization,
                Status = model.Status.ToString(),
                DepotManagerId = model.DepotManagerId,
                Latitude = model.Location?.Latitude,
                Longitude = model.Location?.Longitude
            };

            if (model.Id > 0)
            {
                entity.Id = model.Id;
            }

            return entity;
        }

        public static DepotDto ToDto(Depot entity)
        {
            if (!Enum.TryParse<DepotStatus>(entity.Status, ignoreCase: true, out var status))
                throw new InvalidDepotStatusException(entity.Status);

            var model = new DepotDto()
            {
                Id = entity.Id,
                Name = entity.Name,
                Address = entity.Address,
                Capacity = entity.Capacity,
                CurrentUtilization = entity.CurrentUtilization,
                Status = status.ToString(),
                DepotManagerId = entity.DepotManagerId,
                Latitude = entity.Latitude,
                Longitude = entity.Longitude,
                LastUpdatedAt = entity.LastUpdatedAt
            };
            return model;
        }
    }
}
