using System.Diagnostics;
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
            request.ItemName,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = pagedData.Items.Select(x =>
        {
            var isReusable = string.Equals(x.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            var dto = new InventoryItemDto
            {
                ItemModelId = x.ItemModelId,
                ItemModelName = x.ItemModelName,
                ImageUrl = x.ImageUrl,
                CategoryId = x.CategoryId,
                CategoryName = x.CategoryName,
                ItemType = x.ItemType,
                WeightPerUnit = x.WeightPerUnit,
                VolumePerUnit = x.VolumePerUnit,
                TargetGroups = x.TargetGroups,
                Quantity = isReusable ? null : x.Availability.Quantity,
                TotalReservedQuantity = isReusable ? null : x.Availability.TotalReservedQuantity,
                ReservedForMissionQuantity = isReusable ? null : x.Availability.MissionReservedQuantity,
                ReservedForTransferQuantity = isReusable ? null : x.Availability.TransferReservedQuantity,
                AvailableQuantity = isReusable ? null : x.Availability.AvailableQuantity,
                MeasurementUnit = x.MeasurementUnit,
                Unit = isReusable ? x.Availability.Quantity : null,
                TotalReservedUnit = isReusable ? x.Availability.TotalReservedQuantity : null,
                ReservedForMissionUnit = isReusable ? x.Availability.MissionReservedQuantity : null,
                ReservedForTransferUnit = isReusable ? x.Availability.TransferReservedQuantity : null,
                AvailableUnit = isReusable ? x.Availability.AvailableQuantity : null,
                LastStockedAt = x.LastStockedAt,
                LotCount = isReusable ? null : (x.LotCount > 0 ? x.LotCount : null),
                NearestExpiryDate = isReusable ? null : x.NearestExpiryDate,
                IsExpiringSoon = isReusable || !x.NearestExpiryDate.HasValue
                    ? null
                    : (x.NearestExpiryDate.Value >= now && x.NearestExpiryDate.Value <= now.AddDays(30) ? true : null),
                ReusableBreakdown = x.ReusableBreakdown != null
                    ? new ReusableBreakdownDto
                    {
                        TotalUnits = x.ReusableBreakdown.TotalUnits,
                        AvailableUnits = x.ReusableBreakdown.AvailableUnits,
                        TotalReservedUnits = x.ReusableBreakdown.TotalReservedUnits,
                        ReservedForMissionUnits = x.ReusableBreakdown.ReservedForMissionUnits,
                        ReservedForTransferUnits = x.ReusableBreakdown.ReservedForTransferUnits,
                        InTransitUnits = x.ReusableBreakdown.InTransitUnits,
                        InUseUnits = x.ReusableBreakdown.InUseUnits,
                        MaintenanceUnits = x.ReusableBreakdown.MaintenanceUnits,
                        DecommissionedUnits = x.ReusableBreakdown.DecommissionedUnits,
                        GoodCount = x.ReusableBreakdown.GoodCount,
                        FairCount = x.ReusableBreakdown.FairCount,
                        PoorCount = x.ReusableBreakdown.PoorCount
                    }
                    : null
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
