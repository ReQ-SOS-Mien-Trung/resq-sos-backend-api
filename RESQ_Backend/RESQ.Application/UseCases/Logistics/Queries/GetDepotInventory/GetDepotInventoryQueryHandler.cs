using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotInventory;

public class GetDepotInventoryQueryHandler(
    IDepotRepository depotRepository,
    IDepotInventoryRepository depotInventoryRepository) 
    : IRequestHandler<GetDepotInventoryQuery, PagedResult<InventoryItemDto>>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;

    public async Task<PagedResult<InventoryItemDto>> Handle(GetDepotInventoryQuery request, CancellationToken cancellationToken)
    {
        var depot = await _depotRepository.GetByIdAsync(request.DepotId, cancellationToken);
        if (depot == null)
        {
            throw new NotFoundException($"Không tìm thấy kho cứu trợ với ID: {request.DepotId}");
        }

        var pagedData = await _depotInventoryRepository.GetInventoryPagedAsync(
            request.DepotId,
            request.CategoryIds,
            request.ItemTypes,
            request.TargetGroups,
            request.PageNumber,
            request.PageSize,
            cancellationToken
        );

        var dtos = pagedData.Items.Select(x =>
        {
            bool isReusable = string.Equals(x.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase);
            return new InventoryItemDto
            {
                ItemModelId       = x.ItemModelId,
                ItemModelName     = x.ItemModelName,
                CategoryId        = x.CategoryId,
                CategoryName      = x.CategoryName,
                ItemType          = x.ItemType,
                TargetGroup       = x.TargetGroup,
                // Consumable fields
                Quantity          = isReusable ? null : x.Availability.Quantity,
                ReservedQuantity  = isReusable ? null : x.Availability.ReservedQuantity,
                AvailableQuantity = isReusable ? null : x.Availability.AvailableQuantity,
                // Reusable fields
                Unit              = isReusable ? x.Availability.Quantity : null,
                ReservedUnit      = isReusable ? x.Availability.ReservedQuantity : null,
                AvailableUnit     = isReusable ? x.Availability.AvailableQuantity : null,
                LastStockedAt     = x.LastStockedAt,
                ReusableBreakdown = x.ReusableBreakdown != null ? new ReusableBreakdownDto
                {
                    TotalUnits          = x.ReusableBreakdown.TotalUnits,
                    AvailableUnits      = x.ReusableBreakdown.AvailableUnits,
                    ReservedUnits       = x.ReusableBreakdown.ReservedUnits,
                    InTransitUnits      = x.ReusableBreakdown.InTransitUnits,
                    InUseUnits          = x.ReusableBreakdown.InUseUnits,
                    MaintenanceUnits    = x.ReusableBreakdown.MaintenanceUnits,
                    DecommissionedUnits = x.ReusableBreakdown.DecommissionedUnits,
                    GoodCount           = x.ReusableBreakdown.GoodCount,
                    FairCount           = x.ReusableBreakdown.FairCount,
                    PoorCount           = x.ReusableBreakdown.PoorCount
                } : null
            };
        }).ToList();

        return new PagedResult<InventoryItemDto>(dtos, pagedData.TotalCount, pagedData.PageNumber, pagedData.PageSize);
    }
}
