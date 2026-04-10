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
                WeightCapacity = model.WeightCapacity,
                CurrentWeightUtilization = model.CurrentWeightUtilization,
                Status = model.Status.ToString(),
                LastUpdatedAt = model.LastUpdatedAt,
                ImageUrl = model.ImageUrl
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
            entity.WeightCapacity = model.WeightCapacity;
            entity.CurrentWeightUtilization = model.CurrentWeightUtilization;
            entity.Status = model.Status.ToString();
            entity.LastUpdatedAt = model.LastUpdatedAt;
            entity.ImageUrl = model.ImageUrl;

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
                Capacity = entity.Capacity ?? 0m,
                CurrentUtilization = entity.CurrentUtilization ?? 0m,
                WeightCapacity = entity.WeightCapacity ?? 0m,
                CurrentWeightUtilization = entity.CurrentWeightUtilization ?? 0m,
                Status = status,
                LastUpdatedAt = entity.LastUpdatedAt,
                ImageUrl = entity.ImageUrl
            };

            if (entity.DepotManagers != null && entity.DepotManagers.Count != 0)
            {
                // Updated to map User details into the value object
                var history = entity.DepotManagers.Select(dm => 
                    new DepotManagerAssignment(
                        dm.UserId ?? Guid.Empty,
                        dm.AssignedAt ?? DateTime.MinValue,
                        dm.UnassignedAt,
                        dm.User?.FirstName,
                        dm.User?.LastName,
                        dm.User?.Email,
                        dm.User?.Phone
                    ));
                
                model.AddHistory(history);
            }

            // Map item-level inventory (only items with positive available stock)
            if (entity.SupplyInventories != null && entity.SupplyInventories.Count != 0)
            {
                var lines = entity.SupplyInventories
                    .Where(i => (i.Quantity ?? 0) - (i.MissionReservedQuantity + i.TransferReservedQuantity) > 0)
                    .Select(i => new DepotInventoryLine(
                        i.ItemModelId,
                        i.ItemModel?.Name ?? $"Item #{i.ItemModelId}",
                        i.ItemModel?.Unit,
                        (i.Quantity ?? 0) - (i.MissionReservedQuantity + i.TransferReservedQuantity)
                    ));
                model.SetInventoryLines(lines);
            }

            return model;
        }
    }
}
