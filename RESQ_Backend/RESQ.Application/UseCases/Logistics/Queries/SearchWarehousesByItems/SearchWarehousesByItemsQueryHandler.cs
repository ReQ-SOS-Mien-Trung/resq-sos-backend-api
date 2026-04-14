using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;

/// <summary>
/// Handles <see cref="SearchWarehousesByItemsQuery"/>:
/// 1. Resolves the requesting manager's depot location from the token.
/// 2. Fetches flat (item, depot) rows filtered by quantity â‰¥ requested.
/// 3. Groups into item → depots hierarchy.
/// 4. Sorts each item's depots by straight-line distance from the manager's depot (nearest first).
/// </summary>
public class SearchWarehousesByItemsQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepository)
    : IRequestHandler<SearchWarehousesByItemsQuery, PagedResult<ItemWarehouseAvailabilityDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<PagedResult<ItemWarehouseAvailabilityDto>> Handle(
        SearchWarehousesByItemsQuery request,
        CancellationToken cancellationToken)
    {
        // -- Resolve the manager's depot location -----------------------------
        (double Latitude, double Longitude)? managerLocation = null;

        var managerDepotId = await _depotInventoryRepository
            .GetActiveDepotIdByManagerAsync(request.ManagerUserId, cancellationToken);

        if (managerDepotId.HasValue)
        {
            managerLocation = await _depotInventoryRepository
                .GetDepotLocationAsync(managerDepotId.Value, cancellationToken);
        }

        // -- Fetch paged flat rows from the repository -------------------------
        var (flatRows, totalItemCount) = await _depotInventoryRepository.SearchWarehousesByItemsAsync(
            request.ItemModelIds,
            request.ItemQuantities,
            request.ActiveDepotsOnly,
            managerDepotId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        // -- Group flat rows by relief item and sort depots by proximity -------
        var grouped = flatRows
            .GroupBy(r => r.ItemModelId)
            .Select(g =>
            {
                var first = g.First();
                var unsorted = g.Select(r =>
                {
                    double? distanceKm = null;
                    if (managerLocation.HasValue && r.DepotLatitude.HasValue && r.DepotLongitude.HasValue)
                    {
                        distanceKm = HaversineKm(
                            managerLocation.Value.Latitude,
                            managerLocation.Value.Longitude,
                            r.DepotLatitude.Value,
                            r.DepotLongitude.Value);
                    }

                    return new WarehouseStockDto
                    {
                        DepotId           = r.DepotId,
                        DepotName         = r.DepotName,
                        DepotAddress      = r.DepotAddress,
                        DepotStatus       = r.DepotStatus,
                        TotalQuantity     = r.TotalQuantity,
                        ReservedQuantity  = r.ReservedQuantity,
                        AvailableQuantity = r.AvailableQuantity,
                        LastStockedAt     = r.LastStockedAt,
                        DistanceKm        = distanceKm.HasValue ? Math.Round(distanceKm.Value, 2) : null,
                        ConditionBreakdown = first.ItemType == "Reusable"
                            ? new ReusableConditionDto
                              {
                                  GoodAvailableCount = r.GoodAvailableCount,
                                  FairAvailableCount = r.FairAvailableCount,
                                  PoorAvailableCount = r.PoorAvailableCount
                              }
                            : null
                    };
                }).ToList();

                // Reusable: sort by most Good-condition units first, then by distance.
                // Consumable: sort by distance (nearest first).
                var warehouses = first.ItemType == "Reusable"
                    ? unsorted
                        .OrderByDescending(w => w.ConditionBreakdown!.GoodAvailableCount)
                        .ThenByDescending(w => w.ConditionBreakdown!.FairAvailableCount)
                        .ThenBy(w => w.DistanceKm.HasValue ? 0 : 1)
                        .ThenBy(w => w.DistanceKm)
                        .ToList()
                    : unsorted
                        .OrderBy(w => w.DistanceKm.HasValue ? 0 : 1)
                        .ThenBy(w => w.DistanceKm)
                        .ToList();

                return new ItemWarehouseAvailabilityDto
                {
                    ItemModelId                   = first.ItemModelId,
                    ItemModelName                 = first.ItemModelName,
                    CategoryName                  = first.CategoryName,
                    ItemType                      = first.ItemType,
                    Unit                          = first.Unit,
                    TotalAvailableAcrossWarehouses = warehouses.Sum(w => w.AvailableQuantity),
                    Warehouses                    = warehouses
                };
            })
            .OrderBy(x => x.ItemModelName)
            .ToList();

        return new PagedResult<ItemWarehouseAvailabilityDto>(
            grouped,
            totalItemCount,
            request.PageNumber,
            request.PageSize);
    }

    /// <summary>Haversine formula - returns straight-line distance in kilometres.</summary>
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
