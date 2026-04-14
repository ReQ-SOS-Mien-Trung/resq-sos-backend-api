using System.Diagnostics;
using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventory;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotInventory;

public class GetMyDepotInventoryQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,IDepotInventoryRepository depotInventoryRepository) 
    : IRequestHandler<GetMyDepotInventoryQuery, PagedResult<InventoryItemDto>>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<PagedResult<InventoryItemDto>> Handle(GetMyDepotInventoryQuery request, CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken);
        
        if (!depotId.HasValue)
        {
            throw new NotFoundException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");
        }

        var pagedData = await _depotInventoryRepository.GetInventoryPagedAsync(
            depotId.Value,
            request.CategoryIds,
            request.ItemTypes,
            request.TargetGroups,
            null,
            request.PageNumber,
            request.PageSize,
            cancellationToken
        );

        var dtos = pagedData.Items.Select(x =>
        {
            bool isReusable = string.Equals(x.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;
            var dto = new InventoryItemDto
            {
                ItemModelId       = x.ItemModelId,
                ItemModelName     = x.ItemModelName,
                ImageUrl          = x.ImageUrl,
                CategoryId        = x.CategoryId,
                CategoryName      = x.CategoryName,
                ItemType          = x.ItemType,
                WeightPerUnit     = x.WeightPerUnit,
                VolumePerUnit     = x.VolumePerUnit,
                TargetGroups      = x.TargetGroups,
                // Consumable fields
                Quantity                    = isReusable ? null : x.Availability.Quantity,
                TotalReservedQuantity       = isReusable ? null : x.Availability.TotalReservedQuantity,
                ReservedForMissionQuantity  = isReusable ? null : x.Availability.MissionReservedQuantity,
                ReservedForTransferQuantity = isReusable ? null : x.Availability.TransferReservedQuantity,
                AvailableQuantity           = isReusable ? null : x.Availability.AvailableQuantity,
                // Reusable fields
                Unit                        = isReusable ? x.Availability.Quantity : null,
                TotalReservedUnit           = isReusable ? x.Availability.TotalReservedQuantity : null,
                ReservedForMissionUnit      = isReusable ? x.Availability.MissionReservedQuantity : null,
                ReservedForTransferUnit     = isReusable ? x.Availability.TransferReservedQuantity : null,
                AvailableUnit               = isReusable ? x.Availability.AvailableQuantity : null,
                LastStockedAt     = x.LastStockedAt,
                // Lot summary (consumable only)
                LotCount          = isReusable ? null : (x.LotCount > 0 ? x.LotCount : null),
                NearestExpiryDate = isReusable ? null : x.NearestExpiryDate,
                IsExpiringSoon    = isReusable || !x.NearestExpiryDate.HasValue ? null
                                    : (x.NearestExpiryDate.Value >= now && x.NearestExpiryDate.Value <= now.AddDays(30) ? true : null),
                ReusableBreakdown = x.ReusableBreakdown != null ? new ReusableBreakdownDto
                {
                    TotalUnits               = x.ReusableBreakdown.TotalUnits,
                    AvailableUnits           = x.ReusableBreakdown.AvailableUnits,
                    TotalReservedUnits       = x.ReusableBreakdown.TotalReservedUnits,
                    ReservedForMissionUnits  = x.ReusableBreakdown.ReservedForMissionUnits,
                    ReservedForTransferUnits = x.ReusableBreakdown.ReservedForTransferUnits,
                    InTransitUnits           = x.ReusableBreakdown.InTransitUnits,
                    InUseUnits               = x.ReusableBreakdown.InUseUnits,
                    MaintenanceUnits         = x.ReusableBreakdown.MaintenanceUnits,
                    DecommissionedUnits      = x.ReusableBreakdown.DecommissionedUnits,
                    GoodCount                = x.ReusableBreakdown.GoodCount,
                    FairCount                = x.ReusableBreakdown.FairCount,
                    PoorCount                = x.ReusableBreakdown.PoorCount
                } : null
            };
            Debug.Assert(
                !isReusable ||
                x.Availability.TotalReservedQuantity ==
                    x.Availability.MissionReservedQuantity + x.Availability.TransferReservedQuantity,
                "Invariant violation: TotalReservedQuantity != MissionReservedQuantity + TransferReservedQuantity");
            return dto;
        }).ToList();

        return new PagedResult<InventoryItemDto>(dtos, pagedData.TotalCount, pagedData.PageNumber, pagedData.PageSize);
    }
}
