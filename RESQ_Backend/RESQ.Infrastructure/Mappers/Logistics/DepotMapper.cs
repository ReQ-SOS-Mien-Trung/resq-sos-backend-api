using NetTopologySuite.Geometries;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Mappers.Resources
{
    public static class DepotMapper
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
                LastUpdatedAt = model.LastUpdatedAt
            };

            if (model.Id > 0)
            {
                entity.Id = model.Id;
            }

            if (model.Location != null)
            {
                entity.Location = new Point(model.Location.Longitude, model.Location.Latitude) { SRID = 4326 };
            }

            if (model.ManagerHistory.Any())
            {
                foreach (var history in model.ManagerHistory)
                {
                    entity.DepotManagers.Add(new DepotManager
                    {
                        UserId = history.UserId,
                        AssignedAt = history.AssignedAt,
                        UnassignedAt = history.UnassignedAt
                    });
                }
            }

            return entity;
        }

        public static void UpdateEntity(Depot entity, DepotModel model)
        {
            entity.Name = model.Name;
            entity.Address = model.Address;
            entity.Capacity = model.Capacity;
            entity.CurrentUtilization = model.CurrentUtilization;
            entity.Status = model.Status.ToString();
            entity.LastUpdatedAt = model.LastUpdatedAt;

            if (model.Location != null)
            {
                entity.Location = new Point(model.Location.Longitude, model.Location.Latitude) { SRID = 4326 };
            }
        }

        public static DepotModel ToDomain(Depot entity)
        {
            if (!Enum.TryParse<DepotStatus>(entity.Status, ignoreCase: true, out var status))
            {
                status = DepotStatus.Closed; 
            }

            GeoLocation? location = null;
            if (entity.Location != null)
            {
                location = new GeoLocation(entity.Location.Y, entity.Location.X);
            }

            var model = new DepotModel
            {
                Id = entity.Id,
                Name = entity.Name ?? string.Empty,
                Address = entity.Address ?? string.Empty,
                Location = location,
                Capacity = entity.Capacity ?? 0,
                CurrentUtilization = entity.CurrentUtilization ?? 0,
                Status = status,
                LastUpdatedAt = entity.LastUpdatedAt
            };

            if (entity.DepotManagers != null && entity.DepotManagers.Count != 0)
            {
                var history = entity.DepotManagers.Select(dm => 
                    new DepotManagerAssignment(
                        dm.UserId ?? Guid.Empty,
                        dm.AssignedAt ?? DateTime.MinValue,
                        dm.UnassignedAt
                    ));
                
                model.AddHistory(history);
            }

            return model;
        }
    }
}
