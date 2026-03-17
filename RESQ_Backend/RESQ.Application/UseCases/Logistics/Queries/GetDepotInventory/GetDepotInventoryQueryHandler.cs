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

        var dtos = pagedData.Items.Select(x => new InventoryItemDto
        {
            ItemModelId       = x.ItemModelId,
            ItemModelName     = x.ItemModelName,
            CategoryId        = x.CategoryId,
            CategoryName      = x.CategoryName,
            ItemType          = x.ItemType,
            TargetGroup       = x.TargetGroup,
            Quantity          = x.Availability.Quantity,
            ReservedQuantity  = x.Availability.ReservedQuantity,
            AvailableQuantity = x.Availability.AvailableQuantity,
            LastStockedAt     = x.LastStockedAt,
            ReusableBreakdown = x.ReusableBreakdown != null ? new ReusableBreakdownDto
            {
                TotalUnits          = x.ReusableBreakdown.TotalUnits,
                AvailableUnits      = x.ReusableBreakdown.AvailableUnits,
                InUseUnits          = x.ReusableBreakdown.InUseUnits,
                MaintenanceUnits    = x.ReusableBreakdown.MaintenanceUnits,
                DecommissionedUnits = x.ReusableBreakdown.DecommissionedUnits,
                GoodCount           = x.ReusableBreakdown.GoodCount,
                FairCount           = x.ReusableBreakdown.FairCount,
                PoorCount           = x.ReusableBreakdown.PoorCount
            } : null
        }).ToList();

        return new PagedResult<InventoryItemDto>(dtos, pagedData.TotalCount, pagedData.PageNumber, pagedData.PageSize);
    }
}
